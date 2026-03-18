namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to insert one or more rows into a table.
/// </summary>
/// <param name="TableName">Target table name.</param>
/// <param name="ColumnNames">Column names in order.</param>
/// <param name="ValueRows">Rows to insert; each row is values in same order as columns.</param>
public sealed record InsertCommand(
    string TableName,
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<IReadOnlyList<string>> ValueRows);
