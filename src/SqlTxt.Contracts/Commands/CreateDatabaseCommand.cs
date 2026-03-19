namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to create a new database.
/// </summary>
/// <param name="DatabaseName">Name of the database (used as root folder name).</param>
/// <param name="Path">Full or relative path where database will be created.</param>
/// <param name="NumberFormat">Optional numeric format (e.g., "standard").</param>
/// <param name="TextEncoding">Optional text encoding (e.g. utf-8, ascii; UTF-8 supported per adr-003).</param>
/// <param name="DefaultMaxShardSize">Optional database-level default max shard size in bytes (e.g., 20971520 for 20 MB).</param>
/// <param name="StorageBackend">Storage backend: "text" (human-readable) or "binary" (performance). Default: "text".</param>
public sealed record CreateDatabaseCommand(
    string DatabaseName,
    string Path,
    string? NumberFormat = null,
    string? TextEncoding = null,
    long? DefaultMaxShardSize = null,
    string? StorageBackend = null);
