namespace SqlTxt.Contracts;

/// <summary>
/// Optional MVCC metadata appended to physical row encoding when enabled for the database.
/// </summary>
/// <param name="Xmin">Creating transaction id (visible from this xid onward).</param>
/// <param name="Xmax">Invalidating xid; 0 means still valid.</param>
public readonly record struct MvccRowVersions(long Xmin, long Xmax);
