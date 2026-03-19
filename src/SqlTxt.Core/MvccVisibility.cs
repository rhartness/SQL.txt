namespace SqlTxt.Core;

/// <summary>
/// MVCC tuple visibility for auto-commit xids (committed watermark).
/// </summary>
public static class MvccVisibility
{
    /// <summary>
    /// Returns true if version is visible at snapshot (inclusive committed xmin, xmax still valid or deleted after snapshot).
    /// </summary>
    public static bool IsVisibleAtSnapshot(long xmin, long xmax, long snapshotCommittedMax)
    {
        if (xmin > snapshotCommittedMax)
            return false;
        if (xmax == 0)
            return true;
        return xmax > snapshotCommittedMax;
    }

    /// <summary>
    /// Row is eligible for vacuum when invalidated by a committed xid below the watermark.
    /// </summary>
    public static bool IsDeadVersion(long xmax, long vacuumBelowExclusive)
    {
        return xmax > 0 && xmax < vacuumBelowExclusive;
    }
}
