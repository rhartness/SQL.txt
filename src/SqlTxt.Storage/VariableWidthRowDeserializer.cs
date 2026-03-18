using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Storage;

/// <summary>
/// Deserializes rows from length-prefixed variable-width format.
/// Format: A|_RowId|len1:val1|len2:val2|...
/// </summary>
public sealed class VariableWidthRowDeserializer : IRowDeserializer
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
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pos = 0;
        var fieldIndex = 0;
        var usesRowId = table.PrimaryKey.Count > 0 || table.ForeignKeys.Count > 0 || table.UniqueColumns.Count > 0;
        var totalFields = usesRowId ? 1 + table.Columns.Count : table.Columns.Count;

        while (pos < data.Length && fieldIndex < totalFields)
        {
            var (value, nextPos) = ParseLengthPrefixedField(data, pos);
            if (usesRowId && fieldIndex == 0)
                values[TableDefinition.RowIdColumnName] = value.Trim();
            else
            {
                var colIndex = usesRowId ? fieldIndex - 1 : fieldIndex;
                if (colIndex >= 0 && colIndex < table.Columns.Count)
                {
                    var col = table.Columns[colIndex];
                    var decoded = col.Type == ColumnType.Char || col.Type == ColumnType.VarChar
                        ? FieldCodec.Decode(value)
                        : value.Trim();
                    values[col.Name] = decoded;
                }
            }
            pos = nextPos;
            fieldIndex++;
        }

        return new RowData(values);
    }

    /// <summary>
    /// Parses a length-prefixed field: digits, ':', then exactly length chars.
    /// Returns (value, position after field). Position may be past a trailing '|'.
    /// </summary>
    private static (string Value, int NextPos) ParseLengthPrefixedField(string data, int start)
    {
        var i = start;
        var lenStart = i;
        while (i < data.Length && char.IsDigit(data[i]))
            i++;
        if (i == lenStart || i >= data.Length || data[i] != ':')
            throw new StorageException(
                "Invalid variable-width field: expected length:value format. Manual editing may have corrupted the row.",
                fileName: null, rowNumber: null, characterPosition: start);
        var lenStr = data[lenStart..i];
        var len = int.Parse(lenStr);
        i++; // skip ':'
        if (i + len > data.Length)
            throw new StorageException(
                $"Invalid variable-width field: expected {len} chars, found {data.Length - i}. Manual editing may have truncated the value.",
                fileName: null, rowNumber: null, characterPosition: i);
        var value = data.Substring(i, len);
        i += len;
        if (i < data.Length && data[i] == '|')
            i++;
        return (value, i);
    }
}
