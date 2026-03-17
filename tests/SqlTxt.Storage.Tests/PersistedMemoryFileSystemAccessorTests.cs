using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class PersistedMemoryFileSystemAccessorTests
{
    [Fact]
    public async Task RoundTrip_PersistsAndLoads()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "SqlTxtWasm_" + Guid.NewGuid().ToString("N")[..8] + ".wasmdb");
        try
        {
            var fs = new PersistedMemoryFileSystemAccessor(tempFile);
            fs.CreateDirectory("WikiDb/db");
            await fs.WriteAllTextAsync("WikiDb/db/manifest.json", "{\"version\":1}");
            fs.CreateDirectory("WikiDb/Tables/User");
            await fs.WriteAllTextAsync("WikiDb/Tables/User/User.txt", "A|1  data");

            Assert.True(File.Exists(tempFile));

            var fs2 = new PersistedMemoryFileSystemAccessor(tempFile);
            Assert.True(fs2.FileExists("WikiDb/db/manifest.json"));
            var manifest = await fs2.ReadAllTextAsync("WikiDb/db/manifest.json");
            Assert.Equal("{\"version\":1}", manifest);
            var data = await fs2.ReadAllTextAsync("WikiDb/Tables/User/User.txt");
            Assert.Equal("A|1  data", data);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void GetVirtualRootFromPersistencePath_ExtractsName()
    {
        var root = PersistedMemoryFileSystemAccessor.GetVirtualRootFromPersistencePath("C:/data/WikiDb.wasmdb");
        Assert.Equal("WikiDb", root);
    }

    [Fact]
    public void LoadFromNonExistent_CreatesEmpty()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "SqlTxtWasm_" + Guid.NewGuid().ToString("N")[..8] + ".wasmdb");
        try
        {
            var fs = new PersistedMemoryFileSystemAccessor(tempFile);
            fs.CreateDirectory("NewDb/db");
            Assert.True(fs.DirectoryExists("NewDb/db"));
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}
