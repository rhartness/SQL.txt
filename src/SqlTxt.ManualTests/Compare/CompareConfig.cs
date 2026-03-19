namespace SqlTxt.ManualTests.Compare;

/// <summary>
/// Configuration for comparison database connections.
/// LocalDB uses default instance; future DBs (e.g. SQL Server, PostgreSQL) will require connection config.
/// </summary>
public sealed class CompareConfig
{
    /// <summary>
    /// Comparison backend identifier: "localdb", or future values like "sqlserver", "postgres".
    /// </summary>
    public string Backend { get; init; } = "localdb";

    /// <summary>
    /// Connection string for the comparison database. Null for LocalDB (uses default instance).
    /// Future: required for sqlserver, postgres, etc.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Creates config for LocalDB using the default instance. No connection string needed.
    /// </summary>
    public static CompareConfig ForLocalDb() =>
        new() { Backend = "localdb", ConnectionString = null };

    /// <summary>
    /// Creates config for a custom database (future use). Requires connection string.
    /// </summary>
    public static CompareConfig ForCustom(string backend, string connectionString) =>
        new() { Backend = backend, ConnectionString = connectionString };
}
