namespace SqlTxt.Contracts;

/// <summary>
/// Result of a SELECT query.
/// </summary>
/// <param name="ColumnNames">Column names in display order.</param>
/// <param name="Rows">Result rows.</param>
public sealed record QueryResult(
    IReadOnlyList<string> ColumnNames,
    IReadOnlyList<RowData> Rows);
