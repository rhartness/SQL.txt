using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Manages index files in table folder. Format: one line per entry, Value|ShardId|_RowId.
/// Backward compatible with legacy Value|_RowId (parsed as ShardId=0).
/// Phase 3.5: files are kept sorted by key (ordinal); lookup uses binary search.
/// </summary>
public sealed class IndexStore : IIndexStore
{
    private const char KeyPartSeparator = '\x1E';

    private readonly Contracts.IFileSystemAccessor _fs;

    public IndexStore(Contracts.IFileSystemAccessor fs) => _fs = fs;

    public static string FormatCompositeKey(IReadOnlyList<string> values) =>
        values.Count == 1 ? values[0] : string.Join(KeyPartSeparator, values);

    public static string FormatCompositeKeyFromRow(RowData row, IReadOnlyList<string> columnNames)
    {
        var vals = columnNames.Select(c => row.GetValue(c) ?? string.Empty).ToList();
        return FormatCompositeKey(vals);
    }

    internal static string GetKeyPart(string line)
    {
        var idx = line.IndexOf('|');
        return idx >= 0 ? line[..idx] : line;
    }

    public async Task CreateIndexAsync(string databasePath, string tableName, string indexFileName, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);
        await _fs.WriteAllTextAsync(path, "", cancellationToken).ConfigureAwait(false);
    }

    public Task AddIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, int shardId = 0, CancellationToken cancellationToken = default) =>
        AddIndexEntriesAsync(databasePath, tableName, indexFileName, [new IndexEntry(keyValue, rowId, shardId)], cancellationToken);

    public async Task AddIndexEntriesAsync(string databasePath, string tableName, string indexFileName, IReadOnlyList<IndexEntry> entries, CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0)
            return;

        var path = GetIndexPath(databasePath, tableName, indexFileName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);

        var lines = await LoadNormalizedLinesAsync(path, cancellationToken).ConfigureAwait(false);
        foreach (var e in entries)
            lines.Add(FormatEntryLine(e));

        SortIndexLinesByKey(lines);
        await WriteSortedAtomicAsync(path, lines, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlySet<string>> ReadAllKeyPrefixesAsync(string databasePath, string tableName, string indexFileName, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return new HashSet<string>(StringComparer.Ordinal);

        var set = new HashSet<string>(StringComparer.Ordinal);
        await foreach (var line in _fs.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            set.Add(GetKeyPart(line));
        }

        return set;
    }

    public async Task RemoveIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return;

        var lines = await LoadNormalizedLinesAsync(path, cancellationToken).ConfigureAwait(false);
        var prefix = keyValue + "|";
        var kept = lines.Where(l => !l.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        SortIndexLinesByKey(kept);
        await WriteSortedAtomicAsync(path, kept, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveIndexEntryByValueAndRowIdAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return;

        var rowIdStr = rowId.ToString();
        var lines = await LoadNormalizedLinesAsync(path, cancellationToken).ConfigureAwait(false);
        var kept = lines.Where(l =>
        {
            if (string.IsNullOrWhiteSpace(l))
                return true;
            var parts = l.Split('|');
            if (parts.Length < 2)
                return true;
            var keyPart = parts[0];
            var lastPart = parts[^1].Trim();
            return keyPart != keyValue || lastPart != rowIdStr;
        }).ToList();

        SortIndexLinesByKey(kept);
        await WriteSortedAtomicAsync(path, kept, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<long>> LookupByValueAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return Array.Empty<long>();

        var lines = await LoadNormalizedLinesAsync(path, cancellationToken).ConfigureAwait(false);
        await EnsureSortedOnDiskAsync(path, lines, cancellationToken).ConfigureAwait(false);
        return BinarySearchCollectRowIds(lines, keyValue);
    }

    public async Task<bool> ContainsKeyAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return false;

        var lines = await LoadNormalizedLinesAsync(path, cancellationToken).ConfigureAwait(false);
        await EnsureSortedOnDiskAsync(path, lines, cancellationToken).ConfigureAwait(false);
        return BinarySearchAnyKey(lines, keyValue);
    }

    public async Task RebuildIndexAsync(string databasePath, string tableName, TableDefinition table, string indexFileName, IReadOnlyList<string> keyColumns, IAsyncEnumerable<RowData> rows, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);

        var lines = new List<string>();
        await foreach (var row in rows.ConfigureAwait(false))
        {
            var rowIdStr = row.GetValue(TableDefinition.RowIdColumnName);
            if (string.IsNullOrEmpty(rowIdStr) || !long.TryParse(rowIdStr, out var rowId))
                continue;

            var key = FormatCompositeKeyFromRow(row, keyColumns);
            var shardId = GetShardIdFromRow(row);
            lines.Add(FormatEntryLine(new IndexEntry(key, rowId, shardId)));
        }

        SortIndexLinesByKey(lines);
        await WriteSortedAtomicAsync(path, lines, cancellationToken).ConfigureAwait(false);
    }

    private static string FormatEntryLine(IndexEntry e) => $"{e.KeyValue}|{e.ShardId}|{e.RowId}";

    private static int GetShardIdFromRow(RowData row)
    {
        var val = row.GetValue("_ShardId");
        return int.TryParse(val, out var sid) ? sid : 0;
    }

    private async Task<List<string>> LoadNormalizedLinesAsync(string path, CancellationToken cancellationToken)
    {
        if (!_fs.FileExists(path))
            return new List<string>();

        var lines = new List<string>();
        await foreach (var line in _fs.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
        {
            if (!string.IsNullOrWhiteSpace(line))
                lines.Add(line.TrimEnd());
        }
        return lines;
    }

    private static bool IsSortedByKey(IReadOnlyList<string> lines)
    {
        for (var i = 1; i < lines.Count; i++)
        {
            var c = string.Compare(GetKeyPart(lines[i - 1]), GetKeyPart(lines[i]), StringComparison.Ordinal);
            if (c > 0)
                return false;
            if (c == 0 && string.Compare(lines[i - 1], lines[i], StringComparison.Ordinal) > 0)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Sorts index file lines by leading key (before first '|'); tie-break on full line.
    /// Lexicographic sort on the full line is incorrect for binary search (e.g. key "100|" sorts before "10|" by character).
    /// </summary>
    internal static void SortIndexLinesByKey(List<string> lines)
    {
        lines.Sort(static (a, b) =>
        {
            var ka = GetKeyPart(a);
            var kb = GetKeyPart(b);
            var c = string.Compare(ka, kb, StringComparison.Ordinal);
            return c != 0 ? c : string.Compare(a, b, StringComparison.Ordinal);
        });
    }

    private async Task EnsureSortedOnDiskAsync(string path, List<string> lines, CancellationToken cancellationToken)
    {
        if (lines.Count <= 1 || IsSortedByKey(lines))
            return;
        SortIndexLinesByKey(lines);
        await WriteSortedAtomicAsync(path, lines, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteSortedAtomicAsync(string path, IReadOnlyList<string> lines, CancellationToken cancellationToken)
    {
        var tmpPath = path + ".tmp";
        var content = lines.Count > 0 ? string.Join(Environment.NewLine, lines) + Environment.NewLine : "";
        await _fs.WriteAllTextAsync(tmpPath, content, cancellationToken).ConfigureAwait(false);
        _fs.MoveFile(tmpPath, path);
    }

    private static IReadOnlyList<long> BinarySearchCollectRowIds(List<string> lines, string keyValue)
    {
        if (lines.Count == 0)
            return Array.Empty<long>();

        var firstIdx = lines.BinarySearch(0, lines.Count, keyValue, KeyPrefixComparer.Instance);
        if (firstIdx < 0)
            return Array.Empty<long>();

        var prefix = keyValue + "|";
        var start = firstIdx;
        while (start > 0 && lines[start - 1].StartsWith(prefix, StringComparison.Ordinal))
            start--;

        var result = new List<long>();
        for (var i = start; i < lines.Count && lines[i].StartsWith(prefix, StringComparison.Ordinal); i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;
            var rest = line[prefix.Length..].Trim();
            var parts = rest.Split('|');
            if (parts.Length == 1)
            {
                if (long.TryParse(parts[0], out var rid))
                    result.Add(rid);
            }
            else if (parts.Length >= 2 && long.TryParse(parts[^1].Trim(), out var rid2))
            {
                result.Add(rid2);
            }
        }
        return result;
    }

    private static bool BinarySearchAnyKey(List<string> lines, string keyValue)
    {
        if (lines.Count == 0)
            return false;
        var idx = lines.BinarySearch(0, lines.Count, keyValue, KeyPrefixComparer.Instance);
        return idx >= 0;
    }

    private sealed class KeyPrefixComparer : IComparer<string>
    {
        internal static readonly KeyPrefixComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (x is null)
                return y is null ? 0 : -1;
            if (y is null)
                return 1;
            var keyX = GetKeyPart(x);
            return string.Compare(keyX, y, StringComparison.Ordinal);
        }
    }

    private string GetIndexPath(string databasePath, string tableName, string indexFileName)
    {
        var fileName = indexFileName.StartsWith("_") ? $"{tableName}{indexFileName}.txt" : $"{tableName}_{indexFileName}.txt";
        return _fs.Combine(databasePath, "Tables", tableName, fileName);
    }
}
