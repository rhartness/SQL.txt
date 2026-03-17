using SqlTxt.Contracts;
using SqlTxt.Storage;

namespace SqlTxt.Storage.Tests;

public class SchemaStoreTests
{
    [Fact]
    public async Task WriteAndReadSchema_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SqlTxtSchema_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var fs = new FileSystemAccessor();
            fs.CreateDirectory(dir);
            fs.CreateDirectory(Path.Combine(dir, "~System", "schemas"));
            fs.CreateDirectory(Path.Combine(dir, "Tables", "Users"));

            var store = new SchemaStore(fs);
            var table = new TableDefinition("Users", new[]
            {
                new ColumnDefinition("Id", ColumnType.Char, 10),
                new ColumnDefinition("Name", ColumnType.Char, 50)
            });

            await store.WriteSchemaAsync(dir, table);
            var read = await store.ReadSchemaAsync(dir, "Users");

            Assert.NotNull(read);
            Assert.Equal("Users", read.TableName);
            Assert.Equal(2, read.Columns.Count);
            Assert.Equal("Id", read.Columns[0].Name);
            Assert.Equal(ColumnType.Char, read.Columns[0].Type);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
