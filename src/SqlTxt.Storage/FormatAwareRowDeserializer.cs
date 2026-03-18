using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Selects FixedWidth or VariableWidth deserializer based on table RowFormatVersion.
/// Format 1 = fixed-width; Format 2 = variable-width (VARCHAR).
/// </summary>
public sealed class FormatAwareRowDeserializer : IRowDeserializer
{
    private readonly FixedWidthRowDeserializer _fixed = new();
    private readonly VariableWidthRowDeserializer _variable = new();

    public RowData Deserialize(string line, TableDefinition table, out bool isActive)
    {
        return table.RowFormatVersion == TableDefinition.RowFormatVersionVariableWidth
            ? _variable.Deserialize(line, table, out isActive)
            : _fixed.Deserialize(line, table, out isActive);
    }
}
