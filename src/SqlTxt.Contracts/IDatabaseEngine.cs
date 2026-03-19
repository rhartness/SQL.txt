namespace SqlTxt.Contracts;

/// <summary>
/// Main entry point for database operations.
/// </summary>
public interface IDatabaseEngine
{
    /// <summary>
    /// Executes a non-query command (CREATE, INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="commandText">SQL command text.</param>
    /// <param name="databasePath">Path to database (explicit or relative to current directory).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with rows affected.</returns>
    Task<EngineResult> ExecuteAsync(string commandText, string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SELECT query and returns the result.
    /// </summary>
    /// <param name="queryText">SQL SELECT query text.</param>
    /// <param name="databasePath">Path to database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query result with columns and rows.</returns>
    Task<EngineResult> ExecuteQueryAsync(string queryText, string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens or creates a database at the given path.
    /// </summary>
    /// <param name="databasePath">Path to database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task OpenAsync(string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a SQL script (batch separator: ; and GO).
    /// </summary>
    /// <param name="scriptText">Full script text.</param>
    /// <param name="databasePath">Path to database root (parent of database folder for CREATE DATABASE).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ExecuteScriptResult> ExecuteScriptAsync(string scriptText, string databasePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Rebalances shards for a table by redistributing rows to balance shard sizes.
    /// </summary>
    /// <param name="databasePath">Path to database root.</param>
    /// <param name="tableName">Table to rebalance.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of rows processed.</returns>
    Task<int> RebalanceTableAsync(string databasePath, string tableName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the sample Wiki database at the given path.
    /// </summary>
    /// <param name="databasePath">Path where WikiDb folder will be created (parent directory).</param>
    /// <param name="options">Optional build options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<BuildSampleWikiResult> BuildSampleWikiAsync(string databasePath, BuildSampleWikiOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes MVCC row versions that are fully superseded (xmax committed at or before the current watermark).
    /// Uses stream transform omit markers; see ADR-010.
    /// </summary>
    Task<int> VacuumMvccRowsAsync(string databasePath, string tableName, CancellationToken cancellationToken = default);
}
