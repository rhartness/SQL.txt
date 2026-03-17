namespace SqlTxt.Contracts;

/// <summary>
/// Manages index files (PK, FK, secondary indexes) with Value|_RowId format.
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
    /// Appends an index entry: Value|_RowId.
    /// </summary>
    /// <param name="databasePath">Database root path.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="indexFileName">Index file name.</param>
    /// <param name="keyValue">Composite key value (use \x1E to separate multi-column keys).</param>
    /// <param name="rowId">_RowId of the row.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task AddIndexEntryAsync(string databasePath, string tableName, string indexFileName, string keyValue, long rowId, CancellationToken cancellationToken = default);

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
