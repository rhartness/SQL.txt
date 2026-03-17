namespace SqlTxt.Contracts;

/// <summary>
/// Result of a non-query command execution (CREATE, INSERT, UPDATE, DELETE).
/// </summary>
/// <param name="RowsAffected">Number of rows affected (for INSERT, UPDATE, DELETE).</param>
/// <param name="QueryResult">For SELECT, the query result. Null for non-query commands.</param>
/// <param name="Warnings">Non-fatal warnings (e.g., truncation).</param>
public sealed record EngineResult(
    int RowsAffected = 0,
    QueryResult? QueryResult = null,
    IReadOnlyList<string>? Warnings = null);
