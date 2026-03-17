namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to select rows from a table.
/// </summary>
/// <param name="TableName">Source table name.</param>
/// <param name="ColumnNames">Column names to project, or null for SELECT *.</param>
/// <param name="WhereColumn">Column for equality filter, or null for no filter.</param>
/// <param name="WhereValue">Value for equality filter.</param>
public sealed record SelectCommand(
    string TableName,
    IReadOnlyList<string>? ColumnNames,
    string? WhereColumn = null,
    string? WhereValue = null);
