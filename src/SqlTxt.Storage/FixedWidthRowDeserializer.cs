using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Storage;

/// <summary>
/// Deserializes rows from fixed-width format.
/// </summary>
public sealed class FixedWidthRowDeserializer : IRowDeserializer
{
    public RowData Deserialize(string line, TableDefinition table, out bool isActive)
    {
        if (line.Length < 2)
            throw new StorageException(
                "Invalid row format: line too short. Each row must start with A| (active) or D| (deleted). Manual editing may have truncated the line.",
                fileName: null, rowNumber: null, characterPosition: 0);

        var prefix = line[..2];
        isActive = prefix == "A|";
        if (!isActive && prefix != "D|")
            throw new StorageException(
                $"Invalid row marker: expected A| or D|, got '{prefix}'. Manual editing may have corrupted the row status.",
                fileName: null, rowNumber: null, characterPosition: 0);

        var data = line.Length > 2 ? line[2..] : string.Empty;
        var parts = data.Split('|');
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var hasRowId = parts.Length == table.Columns.Count + 1;
        var offset = hasRowId ? 1 : 0;

        if (hasRowId)
            values[TableDefinition.RowIdColumnName] = parts[0].Trim();

        for (var i = 0; i < table.Columns.Count && i + offset < parts.Length; i++)
        {
            var col = table.Columns[i];
            var raw = parts[i + offset].Trim();
            var decoded = col.Type == ColumnType.Char ? FieldCodec.Decode(raw) : raw;
            values[col.Name] = decoded;
        }

        return new RowData(values);
    }
}
