namespace SqlTxt.Contracts;

/// <summary>
/// Definition of an index on a table.
/// </summary>
/// <param name="IndexName">Index name (e.g., IX_Users_Name).</param>
/// <param name="TableName">Table the index is on.</param>
/// <param name="ColumnNames">Columns in index order.</param>
/// <param name="IsUnique">Whether the index enforces uniqueness.</param>
public sealed record IndexDefinition(
    string IndexName,
    string TableName,
    IReadOnlyList<string> ColumnNames,
    bool IsUnique = false);
