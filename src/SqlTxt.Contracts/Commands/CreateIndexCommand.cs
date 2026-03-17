namespace SqlTxt.Contracts.Commands;

/// <summary>
/// Command to create an index on a table.
/// </summary>
/// <param name="IndexName">Index name (e.g., IX_Users_Name).</param>
/// <param name="TableName">Table to index.</param>
/// <param name="ColumnNames">Columns in index order.</param>
/// <param name="IsUnique">Whether the index is unique.</param>
public sealed record CreateIndexCommand(
    string IndexName,
    string TableName,
    IReadOnlyList<string> ColumnNames,
    bool IsUnique = false);
