using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Serializes rows to fixed-width format with A|/D| soft-delete marker.
/// CHAR fields are encoded (escape sequences) and truncated instead of throwing.
/// </summary>
public sealed class FixedWidthRowSerializer : IRowSerializer
{
    public string Serialize(RowData row, TableDefinition table, bool isActive = true, List<string>? warnings = null, string? tableName = null)
    {
        var prefix = isActive ? "A|" : "D|";
        var parts = new List<string>();
        var tbl = tableName ?? table.TableName;

        foreach (var col in table.Columns)
        {
            var value = row.GetValue(col.Name) ?? string.Empty;
            var width = col.StorageWidth;
            var padded = PadValue(value, col, width, warnings, tbl);
            parts.Add(padded);
        }

        return prefix + string.Join("|", parts);
    }

    private static string PadValue(string value, ColumnDefinition col, int width, List<string>? warnings, string tableName)
    {
        return col.Type switch
        {
            ColumnType.Char => PadChar(value, width, warnings, tableName, col.Name),
            ColumnType.Int => PadLeft(value, width),
            ColumnType.TinyInt => PadLeft(value, width),
            ColumnType.BigInt => PadLeft(value, width),
            ColumnType.Bit => value is "1" or "0" ? value : "0",
            ColumnType.Decimal => PadDecimal(value, col, width),
            _ => PadRight(value, width),
        };
    }

    private static string PadChar(string value, int width, List<string>? warnings, string tableName, string columnName)
    {
        var encoded = FieldCodec.Encode(value);
        var truncated = FieldCodec.TruncateToWidth(encoded, width, warnings, tableName, columnName);
        return truncated.PadRight(width);
    }

    private static string PadRight(string value, int width)
    {
        if (value.Length > width)
            value = value[..width];
        return value.PadRight(width);
    }

    private static string PadLeft(string value, int width)
    {
        if (value.Length > width)
            value = value[^width..];
        return value.PadLeft(width);
    }

    private static string PadDecimal(string value, ColumnDefinition col, int width)
    {
        if (value.Length > width)
            value = value[^width..];
        return value.PadLeft(width);
    }
}
