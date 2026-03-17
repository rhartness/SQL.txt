using SqlTxt.Engine;

namespace SqlTxt.IntegrationTests;

public class IntegrationTests
{
    [Fact]
    public async Task FullWorkflow_CreateDbTableInsertSelectUpdateDelete_VerifyFiles()
    {
        var dbName = "SqlTxtInt_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            var dbPath = dir;

            await engine.ExecuteAsync("CREATE TABLE Product (Id CHAR(5), Name CHAR(20), Price INT)", dbPath);
            await engine.ExecuteAsync("INSERT INTO Product (Id, Name, Price) VALUES ('1', 'Widget', '99')", dbPath);
            await engine.ExecuteAsync("INSERT INTO Product (Id, Name, Price) VALUES ('2', 'Gadget', '199')", dbPath);

            var select1 = await engine.ExecuteQueryAsync("SELECT * FROM Product", dbPath);
            Assert.Equal(2, select1.QueryResult!.Rows.Count);

            await engine.ExecuteAsync("UPDATE Product SET Price = '149' WHERE Id = '2'", dbPath);
            var select2 = await engine.ExecuteQueryAsync("SELECT * FROM Product WHERE Id = '2'", dbPath);
            Assert.Single(select2.QueryResult!.Rows);
            Assert.Equal("149", select2.QueryResult.Rows[0].GetValue("Price"));

            await engine.ExecuteAsync("DELETE FROM Product WHERE Id = '1'", dbPath);
            var select3 = await engine.ExecuteQueryAsync("SELECT * FROM Product", dbPath);
            Assert.Single(select3.QueryResult!.Rows);
            Assert.Equal("2", select3.QueryResult.Rows[0].GetValue("Id"));

            var dataFile = Path.Combine(dbPath, "Tables", "Product", "Product.txt");
            Assert.True(File.Exists(dataFile));
            var content = await File.ReadAllTextAsync(dataFile);
            Assert.Contains("A|", content);
            Assert.Contains("D|", content);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
