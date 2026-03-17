using SqlTxt.Engine;

namespace SqlTxt;

/// <summary>
/// Main entry point for the SqlTxt API. Use this to open databases and execute SQL.
/// </summary>
public static partial class SqlTxtApi
{
    private static readonly DatabaseEngine DefaultEngine = new();

    /// <summary>
    /// Executes a non-query command (CREATE, INSERT, UPDATE, DELETE).
    /// </summary>
    public static Task<Contracts.EngineResult> ExecuteAsync(string commandText, string databasePath, CancellationToken cancellationToken = default) =>
        DefaultEngine.ExecuteAsync(commandText, databasePath, cancellationToken);

    /// <summary>
    /// Executes a SELECT query and returns the result.
    /// </summary>
    public static Task<Contracts.EngineResult> ExecuteQueryAsync(string queryText, string databasePath, CancellationToken cancellationToken = default) =>
        DefaultEngine.ExecuteQueryAsync(queryText, databasePath, cancellationToken);

    /// <summary>
    /// Opens or validates a database at the given path.
    /// </summary>
    public static Task OpenAsync(string databasePath, CancellationToken cancellationToken = default) =>
        DefaultEngine.OpenAsync(databasePath, cancellationToken);

    /// <summary>
    /// Executes a SQL script (batch separators: ; and GO).
    /// </summary>
    public static Task<Contracts.ExecuteScriptResult> ExecuteScriptAsync(string scriptText, string databasePath, CancellationToken cancellationToken = default) =>
        DefaultEngine.ExecuteScriptAsync(scriptText, databasePath, cancellationToken);

    /// <summary>
    /// Builds the sample Wiki database at the given path.
    /// </summary>
    /// <param name="databasePath">Parent directory where WikiDb folder will be created.</param>
    /// <param name="options">Optional build options (Verbose, DeleteIfExists).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static Task<Contracts.BuildSampleWikiResult> BuildSampleWikiAsync(string databasePath, Contracts.BuildSampleWikiOptions? options = null, CancellationToken cancellationToken = default) =>
        DefaultEngine.BuildSampleWikiAsync(databasePath, options, cancellationToken);
}
