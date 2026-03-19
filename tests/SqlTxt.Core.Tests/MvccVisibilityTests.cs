using SqlTxt.Core;

namespace SqlTxt.Core.Tests;

public class MvccVisibilityTests
{
    [Theory]
    [InlineData(1, 0, 5, true)]
    [InlineData(5, 0, 5, true)]
    [InlineData(6, 0, 5, false)]
    [InlineData(3, 7, 5, true)]
    [InlineData(3, 5, 5, false)]
    [InlineData(3, 6, 5, true)]
    public void IsVisibleAtSnapshot_MatchesCommittedCutoff(long xmin, long xmax, long snapshot, bool expected)
    {
        Assert.Equal(expected, MvccVisibility.IsVisibleAtSnapshot(xmin, xmax, snapshot));
    }

    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(5, 10, true)]
    [InlineData(10, 10, false)]
    public void IsDeadVersion_UsesExclusiveUpperBound(long xmax, long vacuumBelow, bool expected)
    {
        Assert.Equal(expected, MvccVisibility.IsDeadVersion(xmax, vacuumBelow));
    }
}
