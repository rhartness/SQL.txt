namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to select rows from a table (Phase 4: multi-table JOIN, ORDER BY, GROUP BY, subqueries via <see cref="Extensions"/>).
/// </summary>
/// <param name="TableName">Primary FROM table name.</param>
/// <param name="ColumnNames">Column names to project, or null for SELECT * (ignored when <see cref="Extensions"/>.Projections is set).</param>
/// <param name="WhereColumn">Legacy single equality filter column.</param>
/// <param name="WhereValue">Legacy single equality filter value.</param>
/// <param name="WithNoLock">If true, skip lock acquisition (allows dirty reads).</param>
/// <param name="Extensions">Phase 4 features; null for legacy single-table SELECT.</param>
public sealed record SelectCommand(
    string TableName,
    IReadOnlyList<string>? ColumnNames,
    string? WhereColumn = null,
    string? WhereValue = null,
    bool WithNoLock = false,
    SelectPhase4Extensions? Extensions = null);
