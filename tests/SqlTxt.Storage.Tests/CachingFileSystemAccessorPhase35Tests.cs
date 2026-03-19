using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class CachingFileSystemAccessorPhase35Tests
{
    [Fact]
    public async Task AppendAllTextAsync_UsesInnerAppend_AndExtendsCache()
    {
        var inner = new MemoryFileSystemAccessor();
        inner.CreateDirectory("/data");
        var path = inner.Combine("/data", "f.txt");
        await inner.WriteAllTextAsync(path, "alpha\n");

        var cache = new CachingFileSystemAccessor(inner, maxCachedBytes: 1_000_000);
        _ = await cache.ReadAllBytesAsync(path);

        await cache.AppendAllTextAsync(path, "beta\n");

        var onDisk = await inner.ReadAllTextAsync(path);
        Assert.Equal("alpha\nbeta\n", onDisk.Replace("\r\n", "\n", StringComparison.Ordinal));

        var roundTrip = await cache.ReadAllTextAsync(path);
        Assert.Equal(onDisk.Replace("\r\n", "\n", StringComparison.Ordinal), roundTrip.Replace("\r\n", "\n", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AppendAllBytesAsync_UsesInnerAppend_AndExtendsCache()
    {
        var inner = new MemoryFileSystemAccessor();
        inner.CreateDirectory("/b");
        var path = inner.Combine("/b", "x.bin");
        await inner.WriteAllBytesAsync(path, [1, 2]);

        var cache = new CachingFileSystemAccessor(inner, maxCachedBytes: 1_000_000);
        _ = await cache.ReadAllBytesAsync(path);

        await cache.AppendAllBytesAsync(path, [3, 4]);

        var bytes = await inner.ReadAllBytesAsync(path);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, bytes);

        var cached = await cache.ReadAllBytesAsync(path);
        Assert.Equal(bytes, cached);
    }
}
