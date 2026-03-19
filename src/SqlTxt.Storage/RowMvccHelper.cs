using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Reads MVCC xmin/xmax from <see cref="RowData"/> after deserialization.
/// </summary>
public static class RowMvccHelper
{
    /// <summary>
    /// Default xmin/xmax when no MVCC tail was stored (legacy row).
    /// </summary>
    public static (long Xmin, long Xmax) GetXminXmax(RowData row)
    {
        var xs = row.GetValue(TableDefinition.MvccXminKey);
        var xl = row.GetValue(TableDefinition.MvccXmaxKey);
        var xmin = long.TryParse(xs, out var a) ? a : 1L;
        var xmax = long.TryParse(xl, out var b) ? b : 0L;
        return (xmin, xmax);
    }

    /// <summary>
    /// Builds optional MVCC struct for serializers when both keys are present.
    /// </summary>
    public static MvccRowVersions? ToMvccVersions(RowData row)
    {
        if (row.GetValue(TableDefinition.MvccXminKey) is not { } xs ||
            row.GetValue(TableDefinition.MvccXmaxKey) is not { } xl)
            return null;
        if (!long.TryParse(xs, out var xmin) || !long.TryParse(xl, out var xmax))
            return null;
        return new MvccRowVersions(xmin, xmax);
    }
}
