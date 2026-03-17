namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to update rows in a table.
/// </summary>
/// <param name="TableName">Target table name.</param>
/// <param name="SetClauses">Column-value pairs to set.</param>
/// <param name="WhereColumn">Column for equality filter, or null for no filter.</param>
/// <param name="WhereValue">Value for equality filter.</param>
public sealed record UpdateCommand(
    string TableName,
    IReadOnlyList<(string Column, string Value)> SetClauses,
    string? WhereColumn = null,
    string? WhereValue = null);
