namespace SqlTxt.Contracts;

/// <summary>
/// Allocates monotonic _RowId values per table for index targeting.
/// </summary>
public interface IRowIdSequenceStore
{
    /// <summary>
    /// Gets the next _RowId for the table and increments the sequence.
    /// Must be called under write lock.
    /// </summary>
    Task<long> GetNextAndIncrementAsync(string databasePath, string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Allocates a range of _RowIds in a single read/write cycle. Returns (start, count).
    /// Must be called under write lock.
    /// </summary>
    Task<(long Start, int Count)> GetNextRangeAndIncrementAsync(string databasePath, string tableName, int count, CancellationToken cancellationToken = default);
}
