using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Serializes rows to length-prefixed variable-width format.
/// Format: A|_RowId|len1:val1|len2:val2|... (length-prefixed segments per spec Option B).
/// Values may contain | and :; length defines exact character count.
/// </summary>
public sealed class VariableWidthRowSerializer : IRowSerializer
{
    public string Serialize(RowData row, TableDefinition table, bool isActive = true, List<string>? warnings = null, string? tableName = null, MvccRowVersions? mvcc = null)
    {
        var prefix = isActive ? "A|" : "D|";
        var parts = new List<string>();
        var tbl = tableName ?? table.TableName;

        var usesRowId = table.PrimaryKey.Count > 0 || table.ForeignKeys.Count > 0 || table.UniqueColumns.Count > 0;
        var rowId = row.GetValue(TableDefinition.RowIdColumnName);
        if (usesRowId && rowId != null)
            parts.Add(EncodeField(rowId));

        foreach (var col in table.Columns)
        {
            var value = row.GetValue(col.Name) ?? string.Empty;
            var encoded = col.Type == ColumnType.Char || col.Type == ColumnType.VarChar
                ? FieldCodec.Encode(value)
                : value;
            var truncated = col.Type == ColumnType.VarChar && encoded.Length > col.StorageWidth
                ? FieldCodec.TruncateToWidth(encoded, col.StorageWidth, warnings, tbl, col.Name)
                : col.Type == ColumnType.Char && encoded.Length > col.StorageWidth
                    ? FieldCodec.TruncateToWidth(encoded, col.StorageWidth, warnings, tbl, col.Name)
                    : encoded;
            parts.Add(EncodeField(truncated));
        }

        if (mvcc is { } v)
        {
            parts.Add(EncodeField(v.Xmin.ToString()));
            parts.Add(EncodeField(v.Xmax.ToString()));
        }

        return prefix + string.Join("|", parts);
    }

    /// <summary>
    /// Encodes a field as length:value. Value can contain any character; length defines exact read.
    /// </summary>
    private static string EncodeField(string value)
    {
        return $"{value.Length}:{value}";
    }
}
