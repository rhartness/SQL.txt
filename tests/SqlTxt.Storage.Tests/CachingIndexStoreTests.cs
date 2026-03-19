using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class CachingIndexStoreTests
{
    private readonly MemoryFileSystemAccessor _fs = new();
    private readonly CachingIndexStore _store;

    public CachingIndexStoreTests()
    {
        _store = new CachingIndexStore(new IndexStore(_fs), _fs);
    }

    [Fact]
    public async Task LookupByValueAsync_CacheWarm_UsesBinarySearch()
    {
        var dbPath = "Db";
        var tableName = "T";
        await _store.CreateIndexAsync(dbPath, tableName, "_PK");

        for (var i = 0; i < 20; i++)
            await _store.AddIndexEntryAsync(dbPath, tableName, "_PK", $"k{i:D2}", 100 + i, 0);

        var first = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "k00");
        Assert.Single(first);
        Assert.Equal(100L, first[0]);

        var middle = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "k10");
        Assert.Single(middle);
        Assert.Equal(110L, middle[0]);

        var last = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "k19");
        Assert.Single(last);
        Assert.Equal(119L, last[0]);

        var missing = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "k99");
        Assert.Empty(missing);
    }

    [Fact]
    public async Task LookupByValueAsync_CacheMiss_DelegatesToInner()
    {
        var dbPath = "Db";
        var tableName = "T";
        _fs.CreateDirectory($"{dbPath}/Tables/{tableName}");
        await _fs.WriteAllTextAsync($"{dbPath}/Tables/{tableName}/T_PK.txt", "x|0|42\n");

        var ids = await _store.LookupByValueAsync(dbPath, tableName, "_PK", "x");
        Assert.Single(ids);
        Assert.Equal(42L, ids[0]);
    }
}
