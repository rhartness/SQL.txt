using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Wraps IIndexStore with an in-memory cache of index file content.
/// After Phase 3.5, on-disk index files are sorted; cache mirrors sorted lines.
/// </summary>
public sealed class CachingIndexStore : IIndexStore
{
    private readonly IIndexStore _inner;
    private readonly Contracts.IFileSystemAccessor _fs;
    private readonly ConcurrentDictionary<string, List<string>?> _cache = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object> _locks = new(StringComparer.Ordinal);

    private static string GetKeyPart(string line)
    {
        var idx = line.IndexOf('|');
        return idx >= 0 ? line[..idx] : line;
    }

    public CachingIndexStore(IIndexStore inner, Contracts.IFileSystemAccessor fs)
    {
        _inner = inner;
        _fs = fs;
    }

    public Task CreateIndexAsync(string databasePath, string tableName, string indexFileName, CancellationToken cancellationToken = default)
    {
        InvalidateCache(databasePath, tableName, indexFileName);
        return _inner.CreateIndexAsync(databasePath, tableName, indexFileName, cancellationToken);
    }

    public async Task AddIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, int shardId = 0, CancellationToken cancellationToken = default)
    {
        await _inner.AddIndexEntryAsync(databasePath, tableName, indexFileName, keyValue, rowId, shardId, cancellationToken).ConfigureAwait(false);
        InvalidateCache(databasePath, tableName, indexFileName);
    }

    public async Task AddIndexEntriesAsync(string databasePath, string tableName, string indexFileName, IReadOnlyList<IndexEntry> entries, CancellationToken cancellationToken = default)
    {
        await _inner.AddIndexEntriesAsync(databasePath, tableName, indexFileName, entries, cancellationToken).ConfigureAwait(false);
        InvalidateCache(databasePath, tableName, indexFileName);
    }

    public Task<IReadOnlySet<string>> ReadAllKeyPrefixesAsync(string databasePath, string tableName, string indexFileName, CancellationToken cancellationToken = default) =>
        _inner.ReadAllKeyPrefixesAsync(databasePath, tableName, indexFileName, cancellationToken);

    public async Task RemoveIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        await _inner.RemoveIndexEntryAsync(databasePath, tableName, indexFileName, keyValue, cancellationToken).ConfigureAwait(false);
        InvalidateCache(databasePath, tableName, indexFileName);
    }

    public async Task RemoveIndexEntryByValueAndRowIdAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, CancellationToken cancellationToken = default)
    {
        await _inner.RemoveIndexEntryByValueAndRowIdAsync(databasePath, tableName, indexFileName, keyValue, rowId, cancellationToken).ConfigureAwait(false);
        InvalidateCache(databasePath, tableName, indexFileName);
    }

    public async Task<IReadOnlyList<long>> LookupByValueAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var path = GetIndexPath(databasePath, tableName, indexFileName);
        if (!_fs.FileExists(path))
            return Array.Empty<long>();

        var lines = await GetOrLoadLinesAsync(databasePath, tableName, indexFileName, path, cancellationToken).ConfigureAwait(false);
        return BinarySearchMatches(lines, keyValue);
    }

    /// <summary>
    /// Loads sorted index lines into the LRU-backed cache bucket for this index file.
    /// </summary>
    private async Task<List<string>> GetOrLoadLinesAsync(string databasePath, string tableName, string indexFileName, string path, CancellationToken cancellationToken)
    {
        var key = CacheKey(databasePath, tableName, indexFileName);
        var lockObj = _locks.GetOrAdd(key, _ => new object());
        lock (lockObj)
        {
            if (_cache.TryGetValue(key, out var cached) && cached is not null)
                return cached;
        }

        var content = await _fs.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.TrimEnd())
            .ToList();
        lines.Sort(StringComparer.Ordinal);

        lock (lockObj)
        {
            _cache[key] = lines;
        }
        return lines;
    }

    private static IReadOnlyList<long> BinarySearchMatches(List<string> lines, string keyValue)
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
            else if (parts.Length >= 2 && long.TryParse(parts[^1].Trim(), out var rid))
            {
                result.Add(rid);
            }
        }
        return result;
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

    public async Task<bool> ContainsKeyAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default)
    {
        var ids = await LookupByValueAsync(databasePath, tableName, indexFileName, keyValue, cancellationToken).ConfigureAwait(false);
        return ids.Count > 0;
    }

    public async Task RebuildIndexAsync(string databasePath, string tableName, TableDefinition table, string indexFileName, IReadOnlyList<string> keyColumns, IAsyncEnumerable<RowData> rows, CancellationToken cancellationToken = default)
    {
        await _inner.RebuildIndexAsync(databasePath, tableName, table, indexFileName, keyColumns, rows, cancellationToken).ConfigureAwait(false);
        InvalidateCache(databasePath, tableName, indexFileName);
    }

    private static string CacheKey(string databasePath, string tableName, string indexFileName) =>
        $"{databasePath}\0{tableName}\0{indexFileName}";

    private string GetIndexPath(string databasePath, string tableName, string indexFileName)
    {
        var fileName = indexFileName.StartsWith("_") ? $"{tableName}{indexFileName}.txt" : $"{tableName}_{indexFileName}.txt";
        return _fs.Combine(databasePath, "Tables", tableName, fileName);
    }

    private void InvalidateCache(string databasePath, string tableName, string indexFileName)
    {
        var key = CacheKey(databasePath, tableName, indexFileName);
        _cache.TryRemove(key, out _);
    }
}
