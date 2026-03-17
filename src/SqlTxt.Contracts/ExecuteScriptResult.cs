namespace SqlTxt.Contracts;

/// <summary>
/// Result of executing a SQL script.
/// </summary>
/// <param name="StatementsExecuted">Number of statements executed.</param>
/// <param name="Warnings">Non-fatal warnings from all statements.</param>
/// <param name="QueryResults">Results from SELECT statements in order.</param>
public sealed record ExecuteScriptResult(
    int StatementsExecuted,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<QueryResult> QueryResults);
