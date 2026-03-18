using SqlTxt.Contracts.Exceptions;
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

    [Fact]
    public async Task Phase2_FullWorkflow_PkFkUniqueIndex_Works()
    {
        var dbName = "SqlTxtInt_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            var dbPath = dir;

            await engine.ExecuteAsync("CREATE TABLE User (Id CHAR(5) PRIMARY KEY, Name CHAR(20))", dbPath);
            await engine.ExecuteAsync("CREATE TABLE Page (Id CHAR(5) PRIMARY KEY, Title CHAR(30), Slug CHAR(30), CreatedById CHAR(5), FOREIGN KEY (CreatedById) REFERENCES User(Id))", dbPath);
            await engine.ExecuteAsync("CREATE INDEX IX_Page_Slug ON Page(Slug)", dbPath);

            await engine.ExecuteAsync("INSERT INTO User (Id, Name) VALUES ('1', 'Alice')", dbPath);
            await engine.ExecuteAsync("INSERT INTO Page (Id, Title, Slug, CreatedById) VALUES ('1', 'Home', 'home', '1')", dbPath);
            await engine.ExecuteAsync("INSERT INTO Page (Id, Title, Slug, CreatedById) VALUES ('2', 'About', 'about', '1')", dbPath);

            var bySlug = await engine.ExecuteQueryAsync("SELECT * FROM Page WHERE Slug = 'about'", dbPath);
            Assert.NotNull(bySlug.QueryResult);
            Assert.Single(bySlug.QueryResult.Rows);
            Assert.Equal("About", bySlug.QueryResult.Rows[0].GetValue("Title"));

            var ex = await Assert.ThrowsAsync<ConstraintViolationException>(
                () => engine.ExecuteAsync("INSERT INTO Page (Id, Title, Slug, CreatedById) VALUES ('3', 'X', 'orphan', '99')", dbPath));
            Assert.Contains("Foreign key", ex.Message);

            var delEx = await Assert.ThrowsAsync<ConstraintViolationException>(
                () => engine.ExecuteAsync("DELETE FROM User WHERE Id = '1'", dbPath));
            Assert.Contains("Cannot delete", delEx.Message);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task BinaryStorage_CreateDbWithBinary_CreateTableInsertSelect_Works()
    {
        var dbName = "SqlTxtInt_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName} WITH (storageBackend=binary)", tempDir);
            var dbPath = dir;

            await engine.ExecuteAsync("CREATE TABLE Item (Id CHAR(5), Name CHAR(20))", dbPath);
            await engine.ExecuteAsync("INSERT INTO Item (Id, Name) VALUES ('1', 'Alpha')", dbPath);
            await engine.ExecuteAsync("INSERT INTO Item (Id, Name) VALUES ('2', 'Beta')", dbPath);

            var result = await engine.ExecuteQueryAsync("SELECT * FROM Item", dbPath);
            Assert.NotNull(result.QueryResult);
            Assert.Equal(2, result.QueryResult.Rows.Count);
            Assert.Equal("Alpha", result.QueryResult.Rows[0].GetValue("Name"));
            Assert.Equal("Beta", result.QueryResult.Rows[1].GetValue("Name"));

            var dataFile = Path.Combine(dbPath, "Tables", "Item", "Item.bin");
            Assert.True(File.Exists(dataFile));
            Assert.True(new FileInfo(dataFile).Length > 0);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
