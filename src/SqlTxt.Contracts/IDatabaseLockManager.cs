namespace SqlTxt.Contracts;

/// <summary>
/// Manages database access locks. Phase 2: read/write locks; NOLOCK skips read lock.
/// </summary>
public interface IDatabaseLockManager
{
    /// <summary>
    /// Acquires a read lock for the database. Caller must release via Dispose.
    /// Multiple readers allowed; blocks writers. Use NOLOCK to skip.
    /// </summary>
    Task<IAsyncDisposable> AcquireReadLockAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a write lock for the database. Caller must release via Dispose.
    /// Exclusive; blocks readers and writers.
    /// </summary>
    Task<IAsyncDisposable> AcquireWriteLockAsync(string databasePath, CancellationToken cancellationToken = default);
}
