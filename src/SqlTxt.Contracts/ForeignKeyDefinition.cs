namespace SqlTxt.Contracts;

/// <summary>
/// Definition of a foreign key constraint.
/// </summary>
/// <param name="ColumnName">Column in this table that references the parent.</param>
/// <param name="ReferencedTable">Parent table name.</param>
/// <param name="ReferencedColumn">Column in parent table (typically PK).</param>
public sealed record ForeignKeyDefinition(
    string ColumnName,
    string ReferencedTable,
    string ReferencedColumn);
