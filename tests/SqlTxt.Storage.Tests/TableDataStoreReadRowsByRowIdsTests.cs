using SqlTxt.Contracts;
using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class TableDataStoreReadRowsByRowIdsTests
{
    private static TableDefinition CreateTestTable() =>
        new("T", new[]
        {
            new ColumnDefinition("Id", ColumnType.Char, 5),
            new ColumnDefinition("Name", ColumnType.Char, 10)
        }, PrimaryKeyColumns: new[] { "Id" });

    private static async Task SetupTableWithRows(MemoryFileSystemAccessor fs, string dbPath, string tableName, params (long RowId, string Id, string Name)[] rows)
    {
        var table = CreateTestTable();
        var schemaStore = new SchemaStore(fs);
        await schemaStore.WriteSchemaAsync(dbPath, table);

        var serializer = new FixedWidthRowSerializer();
        var lines = rows.Select(r => serializer.Serialize(
            new RowData(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [TableDefinition.RowIdColumnName] = r.RowId.ToString(),
                ["Id"] = r.Id,
                ["Name"] = r.Name
            }), table, isActive: true)).ToList();

        var tableDir = $"{dbPath}/Tables/{tableName}";
        fs.CreateDirectory(tableDir);
        await fs.WriteAllTextAsync($"{tableDir}/{tableName}.txt", string.Join(Environment.NewLine, lines) + Environment.NewLine);
    }

    [Fact]
    public async Task ReadRowsByRowIdsAsync_ReturnsOnlyMatchingRows()
    {
        var fs = new MemoryFileSystemAccessor();
        var dbPath = "Db";
        var tableName = "T";
        await SetupTableWithRows(fs, dbPath, tableName,
            (100, "1", "Alice"),
            (101, "2", "Bob"),
            (102, "3", "Carol"));

        var schemaStore = new SchemaStore(fs);
        var stocStore = new StocStore(fs);
        var store = new TableDataStore(fs, new FixedWidthRowSerializer(), new FixedWidthRowDeserializer(), schemaStore, stocStore: stocStore);

        var rowIds = new HashSet<long> { 100L, 102L };
        var collected = new List<RowData>();
        await foreach (var row in store.ReadRowsByRowIdsAsync(dbPath, tableName, rowIds))
            collected.Add(row);

        Assert.Equal(2, collected.Count);
        var ids = collected.Select(r => r.GetValue("Id")).ToHashSet();
        Assert.Contains("1", ids);
        Assert.Contains("3", ids);
        Assert.DoesNotContain("2", ids);
    }

    [Fact]
    public async Task ReadRowsByRowIdsAsync_EmptyRowIds_ReturnsEmpty()
    {
        var fs = new MemoryFileSystemAccessor();
        var dbPath = "Db";
        var tableName = "T";
        await SetupTableWithRows(fs, dbPath, tableName, (100, "1", "Alice"));

        var schemaStore = new SchemaStore(fs);
        var stocStore = new StocStore(fs);
        var store = new TableDataStore(fs, new FixedWidthRowSerializer(), new FixedWidthRowDeserializer(), schemaStore, stocStore: stocStore);

        var rowIds = new HashSet<long>();
        var collected = new List<RowData>();
        await foreach (var row in store.ReadRowsByRowIdsAsync(dbPath, tableName, rowIds))
            collected.Add(row);

        Assert.Empty(collected);
    }

    [Fact]
    public async Task ReadRowsByRowIdsAsync_MultiShard_StreamsOnlyRelevantShards()
    {
        var fs = new MemoryFileSystemAccessor();
        var dbPath = "Db";
        var tableName = "T";
        var table = CreateTestTable();
        var schemaStore = new SchemaStore(fs);
        await schemaStore.WriteSchemaAsync(dbPath, table);

        var serializer = new FixedWidthRowSerializer();
        var row1 = serializer.Serialize(new RowData(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TableDefinition.RowIdColumnName] = "10",
            ["Id"] = "1",
            ["Name"] = "Shard0"
        }), table, isActive: true);
        var row2 = serializer.Serialize(new RowData(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [TableDefinition.RowIdColumnName] = "20",
            ["Id"] = "2",
            ["Name"] = "Shard1"
        }), table, isActive: true);

        var tableDir = $"{dbPath}/Tables/{tableName}";
        fs.CreateDirectory(tableDir);
        await fs.WriteAllTextAsync($"{tableDir}/{tableName}.txt", row1 + Environment.NewLine);
        await fs.WriteAllTextAsync($"{tableDir}/{tableName}_1.txt", row2 + Environment.NewLine);

        await fs.WriteAllTextAsync($"{tableDir}/{tableName}_STOC.txt",
            "0|10|10|T.txt|1\n1|20|20|T_1.txt|1\n");

        var stocStore = new StocStore(fs);
        var store = new TableDataStore(fs, new FixedWidthRowSerializer(), new FixedWidthRowDeserializer(), schemaStore, stocStore: stocStore);

        var rowIds = new HashSet<long> { 20L };
        var collected = new List<RowData>();
        await foreach (var row in store.ReadRowsByRowIdsAsync(dbPath, tableName, rowIds))
            collected.Add(row);

        Assert.Single(collected);
        Assert.Equal("2", collected[0].GetValue("Id"));
        Assert.Equal("Shard1", collected[0].GetValue("Name"));
    }
}
