using SqlTxt.Contracts;
using SqlTxt.Engine;

Console.WriteLine("SQL.txt Sample App - Embedding Demo");
Console.WriteLine();

var engine = new DatabaseEngine();
var dbName = "SqlTxtSample_" + Guid.NewGuid().ToString("N")[..8];
var tempDir = Path.GetTempPath();
var resolvedPath = Path.GetFullPath(Path.Combine(tempDir, dbName));

try
{
    // Create database
    Console.WriteLine("Creating database...");
    await engine.ExecuteAsync($"CREATE DATABASE {dbName}", tempDir);

    // Create table
    Console.WriteLine("Creating table...");
    await engine.ExecuteAsync(
        "CREATE TABLE Product (Id CHAR(10), Name CHAR(50), Price DECIMAL(10,2))",
        resolvedPath);

    // Insert rows
    Console.WriteLine("Inserting rows...");
    await engine.ExecuteAsync(
        "INSERT INTO Product (Id, Name, Price) VALUES ('1', 'Widget', '9.99')",
        resolvedPath);
    await engine.ExecuteAsync(
        "INSERT INTO Product (Id, Name, Price) VALUES ('2', 'Gadget', '19.99')",
        resolvedPath);

    // Select
    Console.WriteLine("Querying...");
    var result = await engine.ExecuteQueryAsync("SELECT * FROM Product", resolvedPath);
    if (result.QueryResult != null)
    {
        Console.WriteLine();
        Console.WriteLine("Results:");
        foreach (var col in result.QueryResult.ColumnNames)
            Console.Write($"  {col}");
        Console.WriteLine();
        foreach (var row in result.QueryResult.Rows)
        {
            foreach (var col in result.QueryResult.ColumnNames)
                Console.Write($"  {row.GetValue(col) ?? ""}");
            Console.WriteLine();
        }
    }

    Console.WriteLine();
    Console.WriteLine("Sample app completed successfully.");
    Console.WriteLine($"Database location: {resolvedPath}");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 1;
}
finally
{
    if (Directory.Exists(resolvedPath))
    {
        try { Directory.Delete(resolvedPath, recursive: true); } catch { /* ignore */ }
    }
}

return 0;
