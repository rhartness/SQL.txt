using System.Text;
using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Storage;

/// <summary>
/// Deserializes rows from fixed-width binary format.
/// </summary>
public sealed class BinaryRowDeserializer : IBinaryRowDeserializer
{
    private const byte ActiveFlag = 0;

    public int GetRecordSize(TableDefinition table)
    {
        var size = 1 + 8;
        foreach (var col in table.Columns)
            size += GetColumnByteSize(col);
        return size;
    }

    private static int GetColumnByteSize(ColumnDefinition col)
    {
        return col.Type switch
        {
            ColumnType.Char => col.Width ?? 0,
            ColumnType.Int => 4,
            ColumnType.TinyInt => 1,
            ColumnType.BigInt => 8,
            ColumnType.Bit => 1,
            ColumnType.Decimal => 8,
            _ => 0,
        };
    }

    public RowData Deserialize(ReadOnlySpan<byte> data, TableDefinition table, out bool isActive)
    {
        if (data.Length < 9)
            throw new StorageException("Invalid binary row: record too short.", fileName: null, rowNumber: null, characterPosition: null);

        isActive = data[0] == ActiveFlag;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rowId = ReadInt64LittleEndian(data.Slice(1, 8));
        values[TableDefinition.RowIdColumnName] = rowId.ToString();

        var offset = 9;
        foreach (var col in table.Columns)
        {
            var colSize = GetColumnByteSize(col);
            if (offset + colSize > data.Length)
                break;
            var slice = data.Slice(offset, colSize);
            var str = ReadColumnValue(col, slice);
            values[col.Name] = str;
            offset += colSize;
        }

        return new RowData(values);
    }

    private static long ReadInt64LittleEndian(ReadOnlySpan<byte> data)
    {
        long result = 0;
        for (var i = 0; i < 8 && i < data.Length; i++)
            result |= (long)data[i] << (i * 8);
        return result;
    }

    private static string ReadColumnValue(ColumnDefinition col, ReadOnlySpan<byte> data)
    {
        return col.Type switch
        {
            ColumnType.Char => FieldCodec.Decode(Encoding.ASCII.GetString(data).TrimEnd()),
            ColumnType.Int => data.Length >= 4 ? ReadInt32LittleEndian(data).ToString() : "0",
            ColumnType.TinyInt => data.Length >= 1 ? ((sbyte)data[0]).ToString() : "0",
            ColumnType.BigInt => data.Length >= 8 ? ReadInt64LittleEndian(data).ToString() : "0",
            ColumnType.Bit => data.Length >= 1 && data[0] != 0 ? "1" : "0",
            ColumnType.Decimal => data.Length >= 8 ? BitConverter.Int64BitsToDouble(ReadInt64LittleEndian(data)).ToString() : "0",
            _ => "",
        };
    }

    private static int ReadInt32LittleEndian(ReadOnlySpan<byte> data)
    {
        int result = 0;
        for (var i = 0; i < 4 && i < data.Length; i++)
            result |= data[i] << (i * 8);
        return result;
    }
}
