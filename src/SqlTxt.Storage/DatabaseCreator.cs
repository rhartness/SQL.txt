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
    /// <param name="defaultMaxShardSize">Optional database-level default max shard size in bytes (default 20 MB).</param>
    /// <param name="storageBackend">Storage backend: "text" or "binary". Default: "text".</param>
    public void CreateDatabase(string databasePath, string? numberFormat = null, string? textEncoding = null, long? defaultMaxShardSize = null, string? storageBackend = null)
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

        var effectiveDefault = defaultMaxShardSize ?? 20_971_520; // 20 MB default per ADR-007
        var effectiveBackend = NormalizeStorageBackend(storageBackend) ?? "text";
        var manifest = new
        {
            engineVersion = "0.1.0",
            storageFormatVersion = 1,
            numberFormat = numberFormat ?? "standard",
            textEncoding = textEncoding ?? "ascii",
            defaultMaxShardSize = effectiveDefault,
            storageBackend = effectiveBackend
        };

        var manifestPath = _fs.Combine(dbDir, "manifest.json");
        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        _fs.WriteAllTextAsync(manifestPath, json).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Reads the default max shard size from the database manifest.
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Default max shard size in bytes, or null if not set (legacy database).</returns>
    public async Task<long?> GetDefaultMaxShardSizeAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var dbDir = _fs.Combine(databasePath, "db");
        var manifestPath = _fs.Combine(dbDir, "manifest.json");
        if (!_fs.FileExists(manifestPath))
            return null;

        var json = await _fs.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("defaultMaxShardSize", out var prop) && prop.TryGetInt64(out var val))
            return val;
        return null;
    }

    /// <summary>
    /// Reads the storage backend from the database manifest.
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Storage backend ("text" or "binary"), or "text" if not set (legacy database).</returns>
    public async Task<string> GetStorageBackendAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var dbDir = _fs.Combine(databasePath, "db");
        var manifestPath = _fs.Combine(dbDir, "manifest.json");
        if (!_fs.FileExists(manifestPath))
            return "text";

        var json = await _fs.ReadAllTextAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("storageBackend", out var prop))
        {
            var val = prop.GetString();
            return NormalizeStorageBackend(val) ?? "text";
        }
        return "text";
    }

    private static string? NormalizeStorageBackend(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim().ToLowerInvariant();
        return v is "text" or "binary" ? v : null;
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
