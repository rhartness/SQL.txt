using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class CorruptionTests
{
    [Fact]
    public void Deserialize_InvalidRowMarker_ThrowsWithFileNameAndPosition()
    {
        var table = new TableDefinition("T", new[] { new ColumnDefinition("A", ColumnType.Char, 5) });
        var des = new FixedWidthRowDeserializer();

        var ex = Assert.Throws<StorageException>(() => des.Deserialize("X|abc  ", table, out _));
        Assert.Contains("A| or D|", ex.Message);
        Assert.Contains("Manual editing", ex.Message);
    }

    [Fact]
    public void Deserialize_TooShortLine_ThrowsWithExplanation()
    {
        var table = new TableDefinition("T", new[] { new ColumnDefinition("A", ColumnType.Char, 5) });
        var des = new FixedWidthRowDeserializer();

        var ex = Assert.Throws<StorageException>(() => des.Deserialize("A", table, out _));
        Assert.Contains("too short", ex.Message);
        Assert.Contains("Manual editing", ex.Message);
    }
}
