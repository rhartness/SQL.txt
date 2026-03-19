namespace SqlTxt.Contracts;

/// <summary>
/// Monotonic MVCC transaction ids per database (persisted under ~System).
/// </summary>
public interface IMvccXidStore
{
    /// <summary>
    /// Snapshot of last committed xid for SELECT visibility (read committed / snapshot start).
    /// </summary>
    Task<long> GetCommittedXidAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Allocates a new xid for a writing statement (not yet committed for visibility).
    /// </summary>
    Task<long> AllocateXidAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks <paramref name="xid"/> and all lower allocations as committed for read visibility.
    /// </summary>
    Task CommitXidAsync(string databasePath, long xid, CancellationToken cancellationToken = default);
}
