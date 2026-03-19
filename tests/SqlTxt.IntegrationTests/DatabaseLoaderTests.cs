using SqlTxt.Engine;
using SqlTxt.Storage;
using Xunit;

namespace SqlTxt.IntegrationTests;

public class DatabaseLoaderTests
{
    [Fact]
    public async Task LoadIntoMemory_And_Query_Works()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SqlTxt_Loader_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Directory.CreateDirectory(tempDir);
            var dbPath = Path.Combine(tempDir, "TestDb");
            var engine = new DatabaseEngine();
            await engine.ExecuteAsync($"CREATE DATABASE TestDb", tempDir);
            await engine.ExecuteAsync("CREATE TABLE T (Id CHAR(10), X CHAR(20))", dbPath);
            await engine.ExecuteAsync("INSERT INTO T (Id, X) VALUES ('1', 'a')", dbPath);

            var (memory, virtualRoot) = await DatabaseLoader.LoadIntoMemoryAsync(dbPath);
            Assert.Equal("TestDb", virtualRoot);

            var queryEngine = new DatabaseEngine(fs: memory);
            var result = await queryEngine.ExecuteQueryAsync("SELECT * FROM T", virtualRoot);
            Assert.NotNull(result.QueryResult);
            Assert.Single(result.QueryResult.Rows);
            Assert.Equal("1", result.QueryResult.Rows[0].GetValue("Id"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
