using SqlTxt.Contracts;
using SqlTxt.Contracts.Commands;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Parser;

namespace SqlTxt.Parser.Tests;

public class SqlCommandParserTests
{
    private readonly ICommandParser _parser = new SqlCommandParser();

    [Fact]
    public void Parse_CreateDatabase_ReturnsCommand()
    {
        var cmd = _parser.Parse("CREATE DATABASE TestDb");
        Assert.IsType<CreateDatabaseCommand>(cmd);
        var c = (CreateDatabaseCommand)cmd;
        Assert.Equal("TestDb", c.DatabaseName);
    }

    [Fact]
    public void Parse_CreateDatabase_WithStorageBackend_ReturnsCommand()
    {
        var cmd = _parser.Parse("CREATE DATABASE TestDb WITH (storageBackend=binary)");
        Assert.IsType<CreateDatabaseCommand>(cmd);
        var c = (CreateDatabaseCommand)cmd;
        Assert.Equal("TestDb", c.DatabaseName);
        Assert.Equal("binary", c.StorageBackend);
    }

    [Fact]
    public void Parse_CreateDatabase_WithStorageBackendText_ReturnsCommand()
    {
        var cmd = _parser.Parse("CREATE DATABASE TestDb WITH (storageBackend=text)");
        Assert.IsType<CreateDatabaseCommand>(cmd);
        var c = (CreateDatabaseCommand)cmd;
        Assert.Equal("text", c.StorageBackend);
    }

    [Fact]
    public void Parse_CreateTable_ReturnsCommand()
    {
        var cmd = _parser.Parse("CREATE TABLE Users (Id CHAR(10), Name CHAR(50))");
        Assert.IsType<CreateTableCommand>(cmd);
        var c = (CreateTableCommand)cmd;
        Assert.Equal("Users", c.Table.TableName);
        Assert.Equal(2, c.Table.Columns.Count);
        Assert.Equal("Id", c.Table.Columns[0].Name);
        Assert.Equal(ColumnType.Char, c.Table.Columns[0].Type);
        Assert.Equal(10, c.Table.Columns[0].Width);
    }

    [Fact]
    public void Parse_Insert_ReturnsCommand()
    {
        var cmd = _parser.Parse("INSERT INTO Users (Id, Name) VALUES ('1', 'Alice')");
        Assert.IsType<InsertCommand>(cmd);
        var c = (InsertCommand)cmd;
        Assert.Equal("Users", c.TableName);
        Assert.Equal(2, c.ColumnNames.Count);
        Assert.Single(c.ValueRows);
        Assert.Equal("1", c.ValueRows[0][0]);
        Assert.Equal("Alice", c.ValueRows[0][1]);
    }

    [Fact]
    public void Parse_InsertMultiRow_ReturnsCommand()
    {
        var cmd = _parser.Parse("INSERT INTO Users (Id, Name) VALUES ('1', 'Alice'), ('2', 'Bob'), ('3', 'Carol')");
        Assert.IsType<InsertCommand>(cmd);
        var c = (InsertCommand)cmd;
        Assert.Equal("Users", c.TableName);
        Assert.Equal(2, c.ColumnNames.Count);
        Assert.Equal(3, c.ValueRows.Count);
        Assert.Equal("1", c.ValueRows[0][0]);
        Assert.Equal("Alice", c.ValueRows[0][1]);
        Assert.Equal("2", c.ValueRows[1][0]);
        Assert.Equal("Bob", c.ValueRows[1][1]);
        Assert.Equal("3", c.ValueRows[2][0]);
        Assert.Equal("Carol", c.ValueRows[2][1]);
    }

    [Fact]
    public void Parse_SelectStar_ReturnsCommand()
    {
        var cmd = _parser.Parse("SELECT * FROM Users");
        Assert.IsType<SelectCommand>(cmd);
        var c = (SelectCommand)cmd;
        Assert.Equal("Users", c.TableName);
        Assert.Null(c.ColumnNames);
    }

    [Fact]
    public void Parse_SelectWithWhere_ReturnsCommand()
    {
        var cmd = _parser.Parse("SELECT Id, Name FROM Users WHERE Id = '1'");
        Assert.IsType<SelectCommand>(cmd);
        var c = (SelectCommand)cmd;
        Assert.Equal(2, c.ColumnNames!.Count);
        Assert.Equal("Id", c.WhereColumn);
        Assert.Equal("1", c.WhereValue);
    }

    [Fact]
    public void Parse_Update_ReturnsCommand()
    {
        var cmd = _parser.Parse("UPDATE Users SET Name = 'Bob' WHERE Id = '1'");
        Assert.IsType<UpdateCommand>(cmd);
        var c = (UpdateCommand)cmd;
        Assert.Equal("Users", c.TableName);
        Assert.Single(c.SetClauses);
        Assert.Equal("Name", c.SetClauses[0].Item1);
        Assert.Equal("Bob", c.SetClauses[0].Item2);
        Assert.Equal("Id", c.WhereColumn);
        Assert.Equal("1", c.WhereValue);
    }

    [Fact]
    public void Parse_Delete_ReturnsCommand()
    {
        var cmd = _parser.Parse("DELETE FROM Users WHERE Id = '1'");
        Assert.IsType<DeleteCommand>(cmd);
        var c = (DeleteCommand)cmd;
        Assert.Equal("Users", c.TableName);
        Assert.Equal("Id", c.WhereColumn);
        Assert.Equal("1", c.WhereValue);
    }

    [Fact]
    public void Parse_OptionalSemicolon_AcceptsStatement()
    {
        var cmd = _parser.Parse("CREATE TABLE X (A CHAR(1));");
        Assert.IsType<CreateTableCommand>(cmd);
    }

    [Fact]
    public void Parse_InvalidSyntax_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => _parser.Parse("CREATE"));
        Assert.Throws<ValidationException>(() => _parser.Parse("SELECT FROM"));
    }

    [Fact]
    public void Parse_BracketedColumnName_ParsesCorrectly()
    {
        var cmd = _parser.Parse("CREATE TABLE T ([My Column] CHAR(10));");
        Assert.IsType<CreateTableCommand>(cmd);
        var create = (CreateTableCommand)cmd;
        Assert.Single(create.Table.Columns);
        Assert.Equal("My Column", create.Table.Columns[0].Name);
    }

    [Fact]
    public void Parse_CreateTableWithPrimaryKey_ParsesCorrectly()
    {
        var cmd = _parser.Parse("CREATE TABLE T (Id CHAR(10) PRIMARY KEY, Name CHAR(50))");
        Assert.IsType<CreateTableCommand>(cmd);
        var create = (CreateTableCommand)cmd;
        Assert.Single(create.Table.PrimaryKey);
        Assert.Equal("Id", create.Table.PrimaryKey[0]);
    }

    [Fact]
    public void Parse_CreateTableWithForeignKey_ParsesCorrectly()
    {
        var cmd = _parser.Parse("CREATE TABLE Page (Id CHAR(10), UserId CHAR(10), FOREIGN KEY (UserId) REFERENCES User(Id))");
        Assert.IsType<CreateTableCommand>(cmd);
        var create = (CreateTableCommand)cmd;
        Assert.Single(create.Table.ForeignKeys);
        Assert.Equal("UserId", create.Table.ForeignKeys[0].ColumnName);
        Assert.Equal("User", create.Table.ForeignKeys[0].ReferencedTable);
        Assert.Equal("Id", create.Table.ForeignKeys[0].ReferencedColumn);
    }

    [Fact]
    public void Parse_CreateIndex_ParsesCorrectly()
    {
        var cmd = _parser.Parse("CREATE INDEX IX_Users_Name ON Users(Name)");
        Assert.IsType<CreateIndexCommand>(cmd);
        var create = (CreateIndexCommand)cmd;
        Assert.Equal("IX_Users_Name", create.IndexName);
        Assert.Equal("Users", create.TableName);
        Assert.Single(create.ColumnNames);
        Assert.Equal("Name", create.ColumnNames[0]);
    }

    [Fact]
    public void Parse_SelectWithNoLock_ParsesCorrectly()
    {
        var cmd = _parser.Parse("SELECT * FROM Users WITH (NOLOCK)");
        Assert.IsType<SelectCommand>(cmd);
        var select = (SelectCommand)cmd;
        Assert.True(select.WithNoLock);
    }

    [Fact]
    public void Parse_CreateTableWithUnique_ParsesCorrectly()
    {
        var cmd = _parser.Parse("CREATE TABLE T (Id CHAR(10) PRIMARY KEY, Email CHAR(50) UNIQUE)");
        Assert.IsType<CreateTableCommand>(cmd);
        var create = (CreateTableCommand)cmd;
        Assert.Single(create.Table.UniqueColumns);
        Assert.Equal("Email", create.Table.UniqueColumns[0]);
    }
}
