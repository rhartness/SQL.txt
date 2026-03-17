using SqlTxt.Engine;

namespace SqlTxt.Engine.Tests;

public class ScriptSplitterTests
{
    [Fact]
    public void SplitIntoBatches_SingleStatement_ReturnsOneBatch()
    {
        var batches = ScriptSplitter.SplitIntoBatches("CREATE TABLE X (A CHAR(1));");
        Assert.Single(batches);
        Assert.Single(batches[0]);
        Assert.Contains("CREATE TABLE", batches[0][0]);
    }

    [Fact]
    public void SplitIntoBatches_GoSeparator_SplitsBatches()
    {
        var batches = ScriptSplitter.SplitIntoBatches("CREATE TABLE A (X CHAR(1));\nGO\nCREATE TABLE B (Y CHAR(1));");
        Assert.Equal(2, batches.Count);
        Assert.Single(batches[0]);
        Assert.Single(batches[1]);
        Assert.Contains("CREATE TABLE A", batches[0][0]);
        Assert.Contains("CREATE TABLE B", batches[1][0]);
    }

    [Fact]
    public void SplitIntoBatches_Comments_Excluded()
    {
        var batches = ScriptSplitter.SplitIntoBatches("-- comment\nCREATE TABLE X (A CHAR(1));");
        Assert.Single(batches);
        Assert.Single(batches[0]);
        Assert.DoesNotContain("--", batches[0][0]);
    }

    [Fact]
    public void SplitIntoBatches_BlockComment_Excluded()
    {
        var batches = ScriptSplitter.SplitIntoBatches("/* comment */ CREATE TABLE X (A CHAR(1));");
        Assert.Single(batches);
        Assert.DoesNotContain("/*", batches[0][0]);
    }
}
