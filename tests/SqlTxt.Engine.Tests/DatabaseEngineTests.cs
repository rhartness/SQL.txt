using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Engine;

namespace SqlTxt.Engine.Tests;

public class DatabaseEngineTests
{
    [Fact]
    public async Task Execute_CreateDatabase_CreatesStructure()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            Assert.True(Directory.Exists(dir));
            Assert.True(Directory.Exists(Path.Combine(dir, "db")));
            Assert.True(Directory.Exists(Path.Combine(dir, "Tables")));
            Assert.True(Directory.Exists(Path.Combine(dir, "~System")));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_CreateDatabase_WithStorageBackendBinary_WritesManifest()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName} WITH (storageBackend=binary)", tempDir);
            var manifestPath = Path.Combine(dir, "db", "manifest.json");
            Assert.True(File.Exists(manifestPath));
            var json = await File.ReadAllTextAsync(manifestPath);
            Assert.Contains("\"storageBackend\"", json);
            Assert.Contains("\"binary\"", json);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_CreateDatabase_WithTextEncodingUtf8_StoresInManifest()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName} WITH (textEncoding='utf-8')", tempDir);
            var manifestPath = Path.Combine(dir, "db", "manifest.json");
            Assert.True(File.Exists(manifestPath));
            var json = await File.ReadAllTextAsync(manifestPath);
            Assert.Contains("\"textEncoding\"", json);
            Assert.Contains("\"utf-8\"", json);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_CreateTableInsertSelect_EndToEnd()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            var dbPath = dir;

            await engine.ExecuteAsync("CREATE TABLE Users (Id CHAR(5), Name CHAR(10))", dbPath);
            await engine.ExecuteAsync("INSERT INTO Users (Id, Name) VALUES ('1', 'Alice')", dbPath);
            var result = await engine.ExecuteQueryAsync("SELECT * FROM Users", dbPath);

            Assert.NotNull(result.QueryResult);
            Assert.Equal(2, result.QueryResult.ColumnNames.Count);
            Assert.Single(result.QueryResult.Rows);
            Assert.Equal("1", result.QueryResult.Rows[0].GetValue("Id"));
            Assert.Equal("Alice", result.QueryResult.Rows[0].GetValue("Name"));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_Update_ModifiesRow()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            await engine.ExecuteAsync("CREATE TABLE T (Id CHAR(5), V CHAR(10))", dir);
            await engine.ExecuteAsync("INSERT INTO T (Id, V) VALUES ('1', 'a')", dir);
            var r1 = await engine.ExecuteAsync("UPDATE T SET V = 'b' WHERE Id = '1'", dir);
            Assert.Equal(1, r1.RowsAffected);

            var result = await engine.ExecuteQueryAsync("SELECT * FROM T", dir);
            Assert.Equal("b", result.QueryResult!.Rows[0].GetValue("V"));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_Delete_SoftDeletesRow()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            await engine.ExecuteAsync("CREATE TABLE T (Id CHAR(5))", dir);
            await engine.ExecuteAsync("INSERT INTO T (Id) VALUES ('1')", dir);
            var r1 = await engine.ExecuteAsync("DELETE FROM T WHERE Id = '1'", dir);
            Assert.Equal(1, r1.RowsAffected);

            var result = await engine.ExecuteQueryAsync("SELECT * FROM T", dir);
            Assert.Empty(result.QueryResult!.Rows);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_InsertDuplicatePrimaryKey_ThrowsConstraintViolation()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            await engine.ExecuteAsync("CREATE TABLE T (Id CHAR(5) PRIMARY KEY, Name CHAR(10))", dir);
            await engine.ExecuteAsync("INSERT INTO T (Id, Name) VALUES ('1', 'Alice')", dir);
            var ex = await Assert.ThrowsAsync<ConstraintViolationException>(
                () => engine.ExecuteAsync("INSERT INTO T (Id, Name) VALUES ('1', 'Bob')", dir));
            Assert.Contains("Duplicate primary key", ex.Message);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_ForeignKey_ValidatesParentExists()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            await engine.ExecuteAsync("CREATE TABLE User (Id CHAR(5) PRIMARY KEY, Name CHAR(10))", dir);
            await engine.ExecuteAsync("CREATE TABLE Page (Id CHAR(5) PRIMARY KEY, Title CHAR(20), CreatedById CHAR(5), FOREIGN KEY (CreatedById) REFERENCES User(Id))", dir);
            await engine.ExecuteAsync("INSERT INTO User (Id, Name) VALUES ('1', 'Alice')", dir);
            await engine.ExecuteAsync("INSERT INTO Page (Id, Title, CreatedById) VALUES ('1', 'Home', '1')", dir);

            var ex = await Assert.ThrowsAsync<ConstraintViolationException>(
                () => engine.ExecuteAsync("INSERT INTO Page (Id, Title, CreatedById) VALUES ('2', 'About', '99')", dir));
            Assert.Contains("Foreign key", ex.Message);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_DeleteParentWithChildren_ThrowsConstraintViolation()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            await engine.ExecuteAsync("CREATE TABLE User (Id CHAR(5) PRIMARY KEY, Name CHAR(10))", dir);
            await engine.ExecuteAsync("CREATE TABLE Page (Id CHAR(5) PRIMARY KEY, Title CHAR(20), CreatedById CHAR(5), FOREIGN KEY (CreatedById) REFERENCES User(Id))", dir);
            await engine.ExecuteAsync("INSERT INTO User (Id, Name) VALUES ('1', 'Alice')", dir);
            await engine.ExecuteAsync("INSERT INTO Page (Id, Title, CreatedById) VALUES ('1', 'Home', '1')", dir);

            var ex = await Assert.ThrowsAsync<ConstraintViolationException>(
                () => engine.ExecuteAsync("DELETE FROM User WHERE Id = '1'", dir));
            Assert.Contains("Cannot delete", ex.Message);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_InsertDuplicateUnique_ThrowsConstraintViolation()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            await engine.ExecuteAsync("CREATE TABLE T (Id CHAR(5) PRIMARY KEY, Email CHAR(20) UNIQUE)", dir);
            await engine.ExecuteAsync("INSERT INTO T (Id, Email) VALUES ('1', 'a@b.com')", dir);
            var ex = await Assert.ThrowsAsync<ConstraintViolationException>(
                () => engine.ExecuteAsync("INSERT INTO T (Id, Email) VALUES ('2', 'a@b.com')", dir));
            Assert.Contains("Duplicate unique", ex.Message);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Execute_CreateIndex_AndSelectUsesIndex()
    {
        var dbName = "SqlTxtEngine_" + Guid.NewGuid().ToString("N")[..8];
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var dir = Path.GetFullPath(Path.Combine(tempDir, dbName));
        var engine = new DatabaseEngine();
        try
        {
            await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);
            await engine.ExecuteAsync("CREATE TABLE T (Id CHAR(5) PRIMARY KEY, Name CHAR(20))", dir);
            await engine.ExecuteAsync("INSERT INTO T (Id, Name) VALUES ('1', 'Alice')", dir);
            await engine.ExecuteAsync("INSERT INTO T (Id, Name) VALUES ('2', 'Bob')", dir);
            await engine.ExecuteAsync("CREATE INDEX IX_T_Name ON T(Name)", dir);

            var result = await engine.ExecuteQueryAsync("SELECT * FROM T WHERE Name = 'Bob'", dir);
            Assert.NotNull(result.QueryResult);
            Assert.Single(result.QueryResult.Rows);
            Assert.Equal("Bob", result.QueryResult.Rows[0].GetValue("Name"));
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task BuildSampleWiki_CreatesDatabase()
    {
        var tempDir = Path.GetFullPath(Path.GetTempPath());
        var wikiPath = Path.Combine(tempDir, "WikiDb_" + Guid.NewGuid().ToString("N")[..8]);
        var engine = new DatabaseEngine();
        try
        {
            var result = await engine.BuildSampleWikiAsync(wikiPath, new BuildSampleWikiOptions(Verbose: false, DeleteIfExists: true));
            Assert.True(result.StatementsExecuted >= 10);
            Assert.True(Directory.Exists(Path.Combine(wikiPath, "WikiDb")));
            var dbPath = Path.Combine(wikiPath, "WikiDb");
            var qr = await engine.ExecuteQueryAsync("SELECT * FROM Page", dbPath);
            Assert.NotNull(qr.QueryResult);
            Assert.Equal(2, qr.QueryResult.Rows.Count);
        }
        finally
        {
            var dbDir = Path.Combine(wikiPath, "WikiDb");
            if (Directory.Exists(dbDir))
                Directory.Delete(dbDir, recursive: true);
            if (Directory.Exists(wikiPath))
                Directory.Delete(wikiPath, recursive: true);
        }
    }
}
