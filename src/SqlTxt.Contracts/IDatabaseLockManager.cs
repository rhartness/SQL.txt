namespace SqlTxt.Contracts;

/// <summary>
/// Manages database access locks. Schema vs per-table; FK-safe ordered acquisition; NOLOCK skips table read locks.
/// </summary>
public interface IDatabaseLockManager
{
    /// <summary>
    /// Acquires a shared schema lock (allows concurrent DML and SELECT with table locks).
    /// Blocks DDL (<see cref="AcquireWriteLockAsync"/>) until released. Prefer this for DML/SELECT.
    /// </summary>
    Task<IAsyncDisposable> AcquireReadLockAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires an exclusive schema lock for DDL (CREATE TABLE, CREATE INDEX, rebalance, etc.).
    /// Blocks all schema readers and table lock acquisition until released.
    /// </summary>
    Task<IAsyncDisposable> AcquireWriteLockAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Shared locks on each table in ascending name order.
    /// </summary>
    Task<IAsyncDisposable> AcquireTableReadLocksAsync(string databasePath, IReadOnlyList<string> tableNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exclusive locks on each table in ascending name order.
    /// </summary>
    Task<IAsyncDisposable> AcquireTableWriteLocksAsync(string databasePath, IReadOnlyList<string> tableNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// FK-safe ordering: union of tables sorted; exclusive set wins over shared for the same name.
    /// Caller should first hold a schema read lock (<see cref="AcquireReadLockAsync"/>).
    /// </summary>
    Task<IAsyncDisposable> AcquireFkOrderedLocksAsync(
        string databasePath,
        IReadOnlyList<string> sharedTables,
        IReadOnlyList<string> exclusiveTables,
        CancellationToken cancellationToken = default);
}
