namespace SqlTxt.Contracts;

/// <summary>
/// Reads and writes table row data.
/// </summary>
public interface ITableDataStore
{
    /// <summary>
    /// Appends a row to the table data file.
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="row">Row data (active).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="warnings">Optional list to collect truncation warnings.</param>
    /// <param name="mvcc">Optional MVCC xmin/xmax suffix when persisting.</param>
    /// <returns>Shard index and _RowId of the appended row.</returns>
    Task<(int ShardIndex, long RowId)> AppendRowAsync(string databasePath, string tableName, RowData row, CancellationToken cancellationToken = default, List<string>? warnings = null, MvccRowVersions? mvcc = null);

    /// <summary>
    /// Appends many rows in order. Rows must already include <see cref="TableDefinition.RowIdColumnName"/> when the table uses RowIds.
    /// Buffers consecutive rows targeting the same shard into one append (Phase 3.5).
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="rows">Rows to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="warnings">Optional truncation warnings.</param>
    /// <param name="mvcc">Shared MVCC tail for each row in the batch.</param>
    Task<IReadOnlyList<(int ShardIndex, long RowId)>> AppendRowsAsync(string databasePath, string tableName, IReadOnlyList<RowData> rows, CancellationToken cancellationToken = default, List<string>? warnings = null, MvccRowVersions? mvcc = null);

    /// <summary>
    /// Reads all active rows from the table.
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="mvccSnapshotCommitted">When MVCC is enabled and this is set (and query is not NOLOCK), rows not visible at this committed xid are skipped.</param>
    IAsyncEnumerable<RowData> ReadRowsAsync(string databasePath, string tableName, CancellationToken cancellationToken = default, long? mvccSnapshotCommitted = null);

    /// <summary>
    /// Reads only active rows whose _RowId is in the given set. Uses STOC to limit shards when possible.
    /// Short-circuits when all requested rows are found.
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="rowIds">Row ids to load.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="mvccSnapshotCommitted">MVCC visibility filter when set.</param>
    IAsyncEnumerable<RowData> ReadRowsByRowIdsAsync(string databasePath, string tableName, IReadOnlySet<long> rowIds, CancellationToken cancellationToken = default, long? mvccSnapshotCommitted = null);

    /// <summary>
    /// Reads all rows (including deleted) for update/delete operations.
    /// </summary>
    Task<IReadOnlyList<(bool IsActive, RowData Row)>> ReadAllRowsWithStatusAsync(string databasePath, string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all rows in the table data file (used after update/delete).
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="rows">Rows to write (active and deleted).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="warnings">Optional list to collect truncation warnings.</param>
    Task WriteAllRowsAsync(string databasePath, string tableName, IReadOnlyList<(bool IsActive, RowData Row)> rows, CancellationToken cancellationToken = default, List<string>? warnings = null);

    /// <summary>
    /// Streams rows from all shards, applies transform to each, and writes back atomically.
    /// Returns row counts for metadata update.
    /// </summary>
    Task<(int TotalRows, int ActiveRows, int DeletedRows)> StreamTransformRowsAsync(
        string databasePath,
        string tableName,
        Func<(bool IsActive, RowData Row), (bool IsActive, RowData Row)> transform,
        CancellationToken cancellationToken = default,
        List<string>? warnings = null);
}
