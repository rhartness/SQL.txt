namespace SqlTxt.Contracts;

/// <summary>
/// Manages database access locks. Phase 1: single mutex per database.
/// </summary>
public interface IDatabaseLockManager
{
    /// <summary>
    /// Acquires a write lock for the database. Caller must release via Dispose.
    /// </summary>
    /// <param name="databasePath">Path to database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Disposable that releases the lock.</returns>
    Task<IAsyncDisposable> AcquireWriteLockAsync(string databasePath, CancellationToken cancellationToken = default);
}
