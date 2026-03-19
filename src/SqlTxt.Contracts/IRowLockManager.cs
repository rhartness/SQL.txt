namespace SqlTxt.Contracts;

/// <summary>
/// Optional fine-grained row exclusive locks for concurrent writers on the same table (MVCC era).
/// Table-level exclusive locks remain the default coordinator; row locks add same-key serialization.
/// </summary>
public interface IRowLockManager
{
    /// <summary>
    /// Acquires an exclusive lock for the logical row. Release via Dispose.
    /// </summary>
    Task<IAsyncDisposable> AcquireRowExclusiveAsync(
        string databasePath,
        string tableName,
        long rowId,
        CancellationToken cancellationToken = default);
}
