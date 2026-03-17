namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to delete rows from a table (soft delete).
/// </summary>
/// <param name="TableName">Target table name.</param>
/// <param name="WhereColumn">Column for equality filter, or null for no filter.</param>
/// <param name="WhereValue">Value for equality filter.</param>
public sealed record DeleteCommand(
    string TableName,
    string? WhereColumn = null,
    string? WhereValue = null);
