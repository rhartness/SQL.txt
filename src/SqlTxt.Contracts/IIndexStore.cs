namespace SqlTxt.Contracts;

/// <summary>
/// One row in an index file: composite key, physical row id, shard index.
/// </summary>
/// <param name="KeyValue">Composite key (multi-column keys use U+001E separator between parts).</param>
/// <param name="RowId">_RowId of the row.</param>
/// <param name="ShardId">Shard where the row is stored.</param>
public readonly record struct IndexEntry(string KeyValue, long RowId, int ShardId);

/// <summary>
/// Manages index files (PK, FK, secondary indexes) with Value|ShardId|_RowId format.
/// Backward compatible with legacy Value|_RowId (treated as ShardId=0).
/// Lines are kept sorted by key (Phase 3.5) for binary search on disk.
/// </summary>
public interface IIndexStore
{
    /// <summary>
    /// Creates an index file (empty or overwrites existing).
    /// </summary>
    /// <param name="databasePath">Database root path.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="indexFileName">Index file name (e.g., _PK, _FK_Users, _INX_Name_0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task CreateIndexAsync(string databasePath, string tableName, string indexFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends an index entry: Value|ShardId|_RowId.
    /// </summary>
    /// <param name="databasePath">Database root path.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="indexFileName">Index file name.</param>
    /// <param name="keyValue">Composite key value (use \x1E to separate multi-column keys).</param>
    /// <param name="rowId">_RowId of the row.</param>
    /// <param name="shardId">Shard index where row resides (0 for root file).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, int shardId = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds many index entries in one merge/sort/write cycle (Phase 3.5).
    /// </summary>
    Task AddIndexEntriesAsync(string databasePath, string tableName, string indexFileName, IReadOnlyList<IndexEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads all distinct key prefixes (the Value part before the first '|') from an index file.
    /// Used for batched INSERT validation.
    /// </summary>
    Task<IReadOnlySet<string>> ReadAllKeyPrefixesAsync(string databasePath, string tableName, string indexFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes an index entry by key value.
    /// </summary>
    Task RemoveIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a specific index entry by key value and row ID (for non-unique indexes like FK).
    /// </summary>
    Task RemoveIndexEntryByValueAndRowIdAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Looks up _RowIds for a key value. Returns empty if not found.
    /// </summary>
    Task<IReadOnlyList<long>> LookupByValueAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a key value exists in the index.
    /// </summary>
    Task<bool> ContainsKeyAsync(string databasePath, string tableName, string indexFileName, string keyValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebuilds an index by scanning the table and rewriting the index file.
    /// </summary>
    Task RebuildIndexAsync(string databasePath, string tableName, TableDefinition table, string indexFileName, IReadOnlyList<string> keyColumns, IAsyncEnumerable<RowData> rows, CancellationToken cancellationToken = default);
}
