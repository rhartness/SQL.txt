using SqlTxt.Contracts;
using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class RowSerializerTests
{
    [Fact]
    public void Serialize_ActiveRow_HasAPrefix()
    {
        var table = new TableDefinition("T", new[] { new ColumnDefinition("A", ColumnType.Char, 5) });
        var row = new RowData(new Dictionary<string, string> { ["A"] = "x" });
        var ser = new FixedWidthRowSerializer();
        var s = ser.Serialize(row, table, isActive: true);
        Assert.StartsWith("A|", s);
    }

    [Fact]
    public void Serialize_DeletedRow_HasDPrefix()
    {
        var table = new TableDefinition("T", new[] { new ColumnDefinition("A", ColumnType.Char, 5) });
        var row = new RowData(new Dictionary<string, string> { ["A"] = "x" });
        var ser = new FixedWidthRowSerializer();
        var s = ser.Serialize(row, table, isActive: false);
        Assert.StartsWith("D|", s);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTrips()
    {
        var table = new TableDefinition("T", new[]
        {
            new ColumnDefinition("Id", ColumnType.Char, 5),
            new ColumnDefinition("Name", ColumnType.Char, 10)
        });
        var row = new RowData(new Dictionary<string, string> { ["Id"] = "1", ["Name"] = "Alice" });
        var ser = new FixedWidthRowSerializer();
        var des = new FixedWidthRowDeserializer();

        var s = ser.Serialize(row, table);
        var outRow = des.Deserialize(s, table, out var isActive);
        Assert.True(isActive);
        Assert.Equal("1", outRow.GetValue("Id"));
        Assert.Equal("Alice", outRow.GetValue("Name"));
    }

    [Fact]
    public void SerializeAndDeserialize_CharWithNewlinesAndTabs_RoundTrips()
    {
        var table = new TableDefinition("T", new[] { new ColumnDefinition("Content", ColumnType.Char, 50) });
        var content = "Line1\nLine2\tTabbed";
        var row = new RowData(new Dictionary<string, string> { ["Content"] = content });
        var ser = new FixedWidthRowSerializer();
        var des = new FixedWidthRowDeserializer();

        var s = ser.Serialize(row, table);
        var outRow = des.Deserialize(s, table, out var isActive);
        Assert.True(isActive);
        Assert.Equal(content, outRow.GetValue("Content"));
    }

    [Fact]
    public void Serialize_ValueExceedsWidth_TruncatesAndAddsWarning()
    {
        var table = new TableDefinition("T", new[] { new ColumnDefinition("A", ColumnType.Char, 5) });
        var row = new RowData(new Dictionary<string, string> { ["A"] = "abcdefgh" });
        var warnings = new List<string>();
        var ser = new FixedWidthRowSerializer();
        var s = ser.Serialize(row, table, true, warnings, "T");
        Assert.StartsWith("A|", s);
        Assert.Contains("abcde", s);
        Assert.Single(warnings);
        Assert.Contains("Truncated", warnings[0]);
    }

    [Fact]
    public void SerializeAndDeserialize_WithRowId_RoundTrips()
    {
        var table = new TableDefinition("T", new[]
        {
            new ColumnDefinition("Id", ColumnType.Char, 5),
            new ColumnDefinition("Name", ColumnType.Char, 10)
        }, PrimaryKeyColumns: new[] { "Id" });
        var row = new RowData(new Dictionary<string, string>
        {
            [TableDefinition.RowIdColumnName] = "42",
            ["Id"] = "1",
            ["Name"] = "Alice"
        });
        var ser = new FixedWidthRowSerializer();
        var des = new FixedWidthRowDeserializer();

        var s = ser.Serialize(row, table);
        Assert.StartsWith("A|", s);
        Assert.Contains("42", s);
        Assert.Contains("1", s);
        Assert.Contains("Alice", s);
        var outRow = des.Deserialize(s, table, out var isActive);
        Assert.True(isActive);
        Assert.Equal("42", outRow.GetValue(TableDefinition.RowIdColumnName));
        Assert.Equal("1", outRow.GetValue("Id"));
        Assert.Equal("Alice", outRow.GetValue("Name"));
    }

    [Fact]
    public void VariableWidth_SerializeAndDeserialize_RoundTrips()
    {
        var table = new TableDefinition("Notes", new[]
        {
            new ColumnDefinition("Id", ColumnType.Char, 10),
            new ColumnDefinition("Title", ColumnType.VarChar, 100),
            new ColumnDefinition("Body", ColumnType.VarChar, 1000)
        }, PrimaryKeyColumns: new[] { "Id" });
        var row = new RowData(new Dictionary<string, string>
        {
            [TableDefinition.RowIdColumnName] = "1",
            ["Id"] = "1",
            ["Title"] = "Hello",
            ["Body"] = "This is body text"
        });
        var ser = new VariableWidthRowSerializer();
        var des = new VariableWidthRowDeserializer();

        var s = ser.Serialize(row, table);
        Assert.StartsWith("A|", s);
        Assert.Contains("5:Hello", s);
        Assert.Contains("17:This is body text", s);
        var outRow = des.Deserialize(s, table, out var isActive);
        Assert.True(isActive);
        Assert.Equal("1", outRow.GetValue("Id"));
        Assert.Equal("Hello", outRow.GetValue("Title"));
        Assert.Equal("This is body text", outRow.GetValue("Body"));
    }

    [Fact]
    public void VariableWidth_ValueWithPipe_RoundTrips()
    {
        var table = new TableDefinition("T", new[]
        {
            new ColumnDefinition("Id", ColumnType.Char, 5),
            new ColumnDefinition("Text", ColumnType.VarChar, 50)
        }, PrimaryKeyColumns: new[] { "Id" });
        var row = new RowData(new Dictionary<string, string>
        {
            [TableDefinition.RowIdColumnName] = "1",
            ["Id"] = "1",
            ["Text"] = "a|b|c"
        });
        var ser = new VariableWidthRowSerializer();
        var des = new VariableWidthRowDeserializer();

        var s = ser.Serialize(row, table);
        var outRow = des.Deserialize(s, table, out var isActive);
        Assert.True(isActive);
        Assert.Equal("a|b|c", outRow.GetValue("Text"));
    }

    [Fact]
    public void FormatAware_VarcharTable_UsesVariableWidth()
    {
        var table = new TableDefinition("Notes", new[]
        {
            new ColumnDefinition("Id", ColumnType.Char, 10),
            new ColumnDefinition("Title", ColumnType.VarChar, 100)
        });
        var row = new RowData(new Dictionary<string, string> { ["Id"] = "1", ["Title"] = "Hi" });
        var ser = new FormatAwareRowSerializer();
        var s = ser.Serialize(row, table);
        Assert.Contains("2:Hi", s);
    }
}
