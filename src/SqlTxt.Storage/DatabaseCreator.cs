using System.Text.Json;
using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Creates database directory structure and manifest.
/// </summary>
public sealed class DatabaseCreator
{
    private readonly Contracts.IFileSystemAccessor _fs;

    public DatabaseCreator(Contracts.IFileSystemAccessor fs) => _fs = fs;

    /// <summary>
    /// Creates the database root folder and initial structure.
    /// </summary>
    /// <param name="databasePath">Full path to database root (folder containing db/, Tables/, ~System/).</param>
    /// <param name="numberFormat">Optional numeric format.</param>
    /// <param name="textEncoding">Optional text encoding.</param>
    public void CreateDatabase(string databasePath, string? numberFormat = null, string? textEncoding = null)
    {
        var root = _fs.GetFullPath(databasePath);
        _fs.CreateDirectory(root);

        var dbDir = _fs.Combine(root, "db");
        _fs.CreateDirectory(dbDir);

        var tablesDir = _fs.Combine(root, "Tables");
        _fs.CreateDirectory(tablesDir);

        var systemDir = _fs.Combine(root, "~System");
        _fs.CreateDirectory(systemDir);

        var schemasDir = _fs.Combine(systemDir, "schemas");
        _fs.CreateDirectory(schemasDir);

        var manifest = new
        {
            engineVersion = "0.1.0",
            storageFormatVersion = 1,
            numberFormat = numberFormat ?? "standard",
            textEncoding = textEncoding ?? "ascii"
        };

        var manifestPath = _fs.Combine(dbDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        _fs.WriteAllTextAsync(manifestPath, json).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Creates a table folder and initial data file.
    /// </summary>
    public void CreateTableFolder(string databaseRootPath, string tableName)
    {
        var tableDir = _fs.Combine(databaseRootPath, "Tables", tableName);
        _fs.CreateDirectory(tableDir);
        // Data file created on first insert
    }
}
