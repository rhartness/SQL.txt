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
        var workList = new List<string>(parts);
        long xmin = 1, xmax = 0;
        var hasMvccTail = false;
        if (parts.Length >= table.Columns.Count + 2 &&
            long.TryParse(parts[^1].Trim(), out var xm) &&
            long.TryParse(parts[^2].Trim(), out var xn))
        {
            var rest = parts.Length - 2;
            if (rest == table.Columns.Count || rest == table.Columns.Count + 1)
            {
                hasMvccTail = true;
                xmin = xn;
                xmax = xm;
                workList.RemoveRange(workList.Count - 2, 2);
            }
        }

        var work = workList;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hasRowId = work.Count == table.Columns.Count + 1;
        var offset = 0;

        if (hasRowId)
        {
            values[TableDefinition.RowIdColumnName] = work[0].Trim();
            offset = 1;
        }

        for (var i = 0; i < table.Columns.Count && offset + i < work.Count; i++)
        {
            var col = table.Columns[i];
            var raw = work[offset + i].Trim();
            var decoded = col.Type == ColumnType.Char ? FieldCodec.Decode(raw) : raw;
            values[col.Name] = decoded;
        }

        if (hasMvccTail)
        {
            values[TableDefinition.MvccXminKey] = xmin.ToString();
            values[TableDefinition.MvccXmaxKey] = xmax.ToString();
        }

        return new RowData(values);
    }
}
