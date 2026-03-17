namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to insert a row into a table.
/// </summary>
/// <param name="TableName">Target table name.</param>
/// <param name="ColumnNames">Column names in order.</param>
/// <param name="Values">Values in same order as columns.</param>
public sealed record InsertCommand(
    string TableName,
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<string> Values);
