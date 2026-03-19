using SqlTxt.Contracts;
using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class IndexStorePhase35Tests
{
    private readonly MemoryFileSystemAccessor _fs = new();
    private readonly IndexStore _store;

    public IndexStorePhase35Tests() => _store = new IndexStore(_fs);

    [Fact]
    public async Task AddIndexEntriesAsync_MergesAndSorts_OnDisk()
    {
        var db = "D";
        var table = "T";
        await _store.CreateIndexAsync(db, table, "_PK");
        await _store.AddIndexEntriesAsync(db, table, "_PK", new[]
        {
            new IndexEntry("c", 3, 0),
            new IndexEntry("a", 1, 0),
            new IndexEntry("b", 2, 0),
        });

        var path = $"{db}/Tables/{table}/T_PK.txt";
        var text = await _fs.ReadAllTextAsync(path);
        var lines = text.Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.StartsWith("a|", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("b|", lines[1], StringComparison.Ordinal);
        Assert.StartsWith("c|", lines[2], StringComparison.Ordinal);

        var ids = await _store.LookupByValueAsync(db, table, "_PK", "b");
        Assert.Single(ids);
        Assert.Equal(2L, ids[0]);
    }

    [Fact]
    public async Task ReadAllKeyPrefixesAsync_ReturnsDistinctKeys()
    {
        var db = "D";
        var table = "T";
        await _store.CreateIndexAsync(db, table, "_PK");
        await _store.AddIndexEntriesAsync(db, table, "_PK", new[]
        {
            new IndexEntry("k", 1, 0),
            new IndexEntry("k", 2, 0),
        });

        var keys = await _store.ReadAllKeyPrefixesAsync(db, table, "_PK");
        Assert.Single(keys);
        Assert.Contains("k", keys);
    }
}
