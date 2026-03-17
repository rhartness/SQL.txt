using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Manages index files in table folder. Format: one line per entry, Value|_RowId.
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

    public async Task CreateIndexAsync(string databasePath, string tableName, string indexFileName, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);
        await _fs.WriteAllTextAsync(path, "", cancellationToken).ConfigureAwait(false);
    }

    public async Task AddIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);
        var line = $"{keyValue}|{rowId}{Environment.NewLine}";
        await _fs.AppendAllTextAsync(path, line, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return;

        var lines = await _fs.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        var prefix = keyValue + "|";
        var kept = lines.Where(l => !l.StartsWith(prefix, StringComparison.Ordinal)).ToList();

        var tmpPath = path + ".tmp";
        var content = kept.Count > 0 ? string.Join(Environment.NewLine, kept) + Environment.NewLine : "";
        await _fs.WriteAllTextAsync(tmpPath, content, cancellationToken).ConfigureAwait(false);
        _fs.MoveFile(tmpPath, path);
    }

    public async Task RemoveIndexEntryByValueAndRowIdAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return;

        var lineToRemove = $"{keyValue}|{rowId}";
        var lines = await _fs.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);
        var kept = lines.Where(l => l != lineToRemove).ToList();

        var tmpPath = path + ".tmp";
        var content = kept.Count > 0 ? string.Join(Environment.NewLine, kept) + Environment.NewLine : "";
        await _fs.WriteAllTextAsync(tmpPath, content, cancellationToken).ConfigureAwait(false);
        _fs.MoveFile(tmpPath, path);
    }

    public async Task<IReadOnlyList<long>> LookupByValueAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return Array.Empty<long>();

        var prefix = keyValue + "|";
        var result = new List<long>();

        await foreach (var line in _fs.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                var rest = line[prefix.Length..].Trim();
                if (long.TryParse(rest, out var rid))
                    result.Add(rid);
            }
        }

        return result;
    }

    public async Task<bool> ContainsKeyAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var ids = await LookupByValueAsync(databasePath, tableName, indexFileName, keyValue, cancellationToken).ConfigureAwait(false);
        return ids.Count > 0;
    }

    public async Task RebuildIndexAsync(string databasePath, string tableName, TableDefinition table, string indexFileName, IReadOnlyList<string> keyColumns, IAsyncEnumerable<RowData> rows, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmpPath = path + ".tmp";

        var lines = new List<string>();
        await foreach (var row in rows.ConfigureAwait(false))
        {
            var rowIdStr = row.GetValue(TableDefinition.RowIdColumnName);
            if (string.IsNullOrEmpty(rowIdStr) || !long.TryParse(rowIdStr, out var rowId))
                continue;

            var key = FormatCompositeKeyFromRow(row, keyColumns);
            lines.Add($"{key}|{rowId}");
        }

        var content = lines.Count > 0 ? string.Join(Environment.NewLine, lines) + Environment.NewLine : "";
        await _fs.WriteAllTextAsync(tmpPath, content, cancellationToken).ConfigureAwait(false);
        _fs.MoveFile(tmpPath, path);
    }

    private string GetIndexPath(string databasePath, string tableName, string indexFileName)
    {
        var fileName = indexFileName.StartsWith("_") ? $"{tableName}{indexFileName}.txt" : $"{tableName}_{indexFileName}.txt";
        return _fs.Combine(databasePath, "Tables", tableName, fileName);
    }
}
