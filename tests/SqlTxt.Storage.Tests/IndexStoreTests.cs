using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class IndexStoreTests
{
    private readonly MemoryFileSystemAccessor _fs = new();
    private readonly IndexStore _store;

    public IndexStoreTests()
    {
        _store = new IndexStore(_fs);
    }

    [Fact]
    public async Task LookupByValueAsync_EmptyIndex_ReturnsEmpty()
    {
        var dbPath = "Db";
        var tableName = "T";
        await _store.CreateIndexAsync(dbPath, tableName, "_PK");
        var ids = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "x");
        Assert.Empty(ids);
    }

    [Fact]
    public async Task LookupByValueAsync_NonExistentFile_ReturnsEmpty()
    {
        _fs.CreateDirectory("Db/Tables/T");
        var ids = await _store.LookupByValueAsync("Db", "T", "_PK", "x");
        Assert.Empty(ids);
    }

    [Fact]
    public async Task LookupByValueAsync_SingleEntry_FindsRowId()
    {
        var dbPath = "Db";
        var tableName = "T";
        _fs.CreateDirectory($"{dbPath}/Tables/{tableName}");
        await _fs.WriteAllTextAsync($"{dbPath}/Tables/{tableName}/T_PK.txt", "key1|0|100\n");
        var ids = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "key1");
        Assert.Single(ids);
        Assert.Equal(100L, ids[0]);
    }

    [Fact]
    public async Task LookupByValueAsync_MultipleEntriesForSameKey_ReturnsAllRowIds()
    {
        var dbPath = "Db";
        var tableName = "T";
        _fs.CreateDirectory($"{dbPath}/Tables/{tableName}");
        await _fs.WriteAllTextAsync($"{dbPath}/Tables/{tableName}/T_PK.txt", "dup|0|10\ndup|0|20\ndup|1|30\n");
        var ids = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "dup");
        Assert.Equal(3, ids.Count);
        Assert.Contains(10L, ids);
        Assert.Contains(20L, ids);
        Assert.Contains(30L, ids);
    }

    [Fact]
    public async Task LookupByValueAsync_BinarySearchFindsFirstMiddleLast()
    {
        var dbPath = "Db";
        var tableName = "T";
        _fs.CreateDirectory($"{dbPath}/Tables/{tableName}");
        var lines = new List<string>();
        for (var i = 0; i < 15; i++)
            lines.Add($"k{i:D2}|0|{100 + i}");
        lines.Sort(StringComparer.Ordinal);
        await _fs.WriteAllTextAsync($"{dbPath}/Tables/{tableName}/T_PK.txt", string.Join("\n", lines) + "\n");

        var first = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "k00");
        Assert.Single(first);
        Assert.Equal(100L, first[0]);

        var middle = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "k07");
        Assert.Single(middle);
        Assert.Equal(107L, middle[0]);

        var last = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "k14");
        Assert.Single(last);
        Assert.Equal(114L, last[0]);
    }

    [Fact]
    public async Task LookupByValueAsync_NonExistentKey_ReturnsEmpty()
    {
        var dbPath = "Db";
        var tableName = "T";
        _fs.CreateDirectory($"{dbPath}/Tables/{tableName}");
        await _fs.WriteAllTextAsync($"{dbPath}/Tables/{tableName}/T_PK.txt", "a|0|1\nb|0|2\nc|0|3\n");
        var ids = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "x");
        Assert.Empty(ids);
    }

    [Fact]
    public async Task LookupByValueAsync_LegacyFormatValueRowId_ParsesCorrectly()
    {
        var dbPath = "Db";
        var tableName = "T";
        _fs.CreateDirectory($"{dbPath}/Tables/{tableName}");
        await _fs.WriteAllTextAsync($"{dbPath}/Tables/{tableName}/T_PK.txt", "legacy|42\n");
        var ids = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "legacy");
        Assert.Single(ids);
        Assert.Equal(42L, ids[0]);
    }
}
