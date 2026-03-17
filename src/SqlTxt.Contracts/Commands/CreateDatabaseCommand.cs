namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to create a new database.
/// </summary>
/// <param name="DatabaseName">Name of the database (used as root folder name).</param>
/// <param name="Path">Full or relative path where database will be created.</param>
/// <param name="NumberFormat">Optional numeric format (e.g., "standard").</param>
/// <param name="TextEncoding">Optional text encoding (fixed-width only).</param>
public sealed record CreateDatabaseCommand(
    string DatabaseName,
    string Path,
    string? NumberFormat = null,
    string? TextEncoding = null);
