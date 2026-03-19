using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Wraps IMetadataStore with an in-memory cache of table metadata.
/// Eliminates repeated disk reads for ReadMetadataAsync.
/// Write-through: UpdateMetadataAsync writes to disk and updates the cache.
/// </summary>
public sealed class CachingMetadataStore : IMetadataStore
{
    private readonly IMetadataStore _inner;
    private readonly ConcurrentDictionary<string, (long RowCount, long ActiveRowCount, long DeletedRowCount)> _cache = new(StringComparer.Ordinal);

    public CachingMetadataStore(IMetadataStore inner)
    {
        _inner = inner;
    }

    public async Task UpdateMetadataAsync(string databasePath, string tableName, long rowCount, long activeRowCount, long deletedRowCount, CancellationToken cancellationToken = default)
    {
        await _inner.UpdateMetadataAsync(databasePath, tableName, rowCount, activeRowCount, deletedRowCount, cancellationToken).ConfigureAwait(false);
        var key = CacheKey(databasePath, tableName);
        _cache[key] = (rowCount, activeRowCount, deletedRowCount);
    }

    public async Task<(long RowCount, long ActiveRowCount, long DeletedRowCount)> ReadMetadataAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(databasePath, tableName);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var result = await _inner.ReadMetadataAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false);
        _cache[key] = result;
        return result;
    }

    /// <summary>
    /// Invalidates the cache for a table (e.g., when table is dropped).
    /// </summary>
    internal void Invalidate(string databasePath, string tableName)
    {
        var key = CacheKey(databasePath, tableName);
        _cache.TryRemove(key, out _);
    }

    private static string CacheKey(string databasePath, string tableName) =>
        $"{databasePath}\0{tableName}";
}
