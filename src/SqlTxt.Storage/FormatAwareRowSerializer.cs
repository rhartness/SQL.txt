using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Selects FixedWidth or VariableWidth serializer based on table RowFormatVersion.
/// Format 1 = fixed-width; Format 2 = variable-width (VARCHAR).
/// </summary>
public sealed class FormatAwareRowSerializer : IRowSerializer
{
    private readonly FixedWidthRowSerializer _fixed = new();
    private readonly VariableWidthRowSerializer _variable = new();

    public string Serialize(RowData row, TableDefinition table, bool isActive = true, List<string>? warnings = null, string? tableName = null)
    {
        return table.RowFormatVersion == TableDefinition.RowFormatVersionVariableWidth
            ? _variable.Serialize(row, table, isActive, warnings, tableName)
            : _fixed.Serialize(row, table, isActive, warnings, tableName);
    }
}
