namespace SqlTxt.Contracts;

/// <summary>
/// Reads and writes table metadata (row counts, etc.).
/// </summary>
public interface IMetadataStore
{
    /// <summary>
    /// Updates table metadata after insert/update/delete.
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table name.</param>
    /// <param name="rowCount">Total row count.</param>
    /// <param name="activeRowCount">Active (non-deleted) row count.</param>
    /// <param name="deletedRowCount">Deleted row count.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UpdateMetadataAsync(string databasePath, string tableName, long rowCount, long activeRowCount, long deletedRowCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads table metadata.
    /// </summary>
    Task<(long RowCount, long ActiveRowCount, long DeletedRowCount)> ReadMetadataAsync(string databasePath, string tableName, CancellationToken cancellationToken = default);
}
