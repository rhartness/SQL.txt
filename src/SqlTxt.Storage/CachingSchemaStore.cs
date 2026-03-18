using System.Collections.Concurrent;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Wraps an ISchemaStore with an in-memory cache to reduce file I/O on repeated reads.
/// Invalidates cache entries when schema is written.
/// </summary>
public sealed class CachingSchemaStore : ISchemaStore
{
    private readonly ISchemaStore _inner;
    private readonly ConcurrentDictionary<string, TableDefinition?> _cache = new();

    public CachingSchemaStore(ISchemaStore inner) => _inner = inner;

    private static string CacheKey(string databasePath, string tableName) =>
        databasePath + "\0" + tableName;

    public async Task WriteSchemaAsync(string databasePath, TableDefinition table, CancellationToken cancellationToken = default)
    {
        await _inner.WriteSchemaAsync(databasePath, table, cancellationToken).ConfigureAwait(false);
        _cache.TryRemove(CacheKey(databasePath, table.TableName), out _);
    }

    public async Task<TableDefinition?> ReadSchemaAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(databasePath, tableName);
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        var schema = await _inner.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false);
        if (schema != null)
            _cache.TryAdd(key, schema);
        return schema;
    }

    public Task<IReadOnlyList<string>> GetTableNamesAsync(string databasePath, CancellationToken cancellationToken = default) =>
        _inner.GetTableNamesAsync(databasePath, cancellationToken);
}
