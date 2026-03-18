using System.Text;
using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Storage;

/// <summary>
/// Serializes rows to fixed-width binary format. Record layout: 1 byte flag, 8 bytes RowId, then fixed-width column values.
/// </summary>
public sealed class BinaryRowSerializer : IBinaryRowSerializer
{
    private const byte ActiveFlag = 0;
    private const byte DeletedFlag = 1;

    public int GetRecordSize(TableDefinition table)
    {
        var size = 1 + 8; // flag + RowId
        foreach (var col in table.Columns)
            size += GetColumnByteSize(col);
        return size;
    }

    private static int GetColumnByteSize(ColumnDefinition col)
    {
        return col.Type switch
        {
            ColumnType.Char => col.Width ?? 0,
            ColumnType.VarChar => col.Width ?? 0,
            ColumnType.Int => 4,
            ColumnType.TinyInt => 1,
            ColumnType.BigInt => 8,
            ColumnType.Bit => 1,
            ColumnType.Decimal => 8,
            _ => 0,
        };
    }

    public byte[] Serialize(RowData row, TableDefinition table, bool isActive = true, List<string>? warnings = null, string? tableName = null)
    {
        var size = GetRecordSize(table);
        var buffer = new byte[size];
        var offset = 0;

        buffer[offset++] = isActive ? ActiveFlag : DeletedFlag;

        var rowId = row.GetValue(TableDefinition.RowIdColumnName);
        var rowIdVal = long.TryParse(rowId, out var rid) ? rid : 0L;
        WriteInt64LittleEndian(buffer, offset, rowIdVal);
        offset += 8;

        foreach (var col in table.Columns)
        {
            var value = row.GetValue(col.Name) ?? string.Empty;
            var colSize = GetColumnByteSize(col);
            WriteColumnValue(buffer, offset, col, value, colSize, warnings, tableName ?? table.TableName);
            offset += colSize;
        }

        return buffer;
    }

    private static void WriteInt64LittleEndian(byte[] buffer, int offset, long value)
    {
        for (var i = 0; i < 8; i++)
        {
            buffer[offset + i] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }

    private static void WriteColumnValue(byte[] buffer, int offset, ColumnDefinition col, string value, int colSize, List<string>? warnings, string tableName)
    {
        switch (col.Type)
        {
            case ColumnType.Char:
            case ColumnType.VarChar:
                var encoded = FieldCodec.Encode(value);
                var truncated = FieldCodec.TruncateToWidth(encoded, colSize, warnings, tableName, col.Name);
                var bytes = Encoding.ASCII.GetBytes(truncated.PadRight(colSize));
                Buffer.BlockCopy(bytes, 0, buffer, offset, Math.Min(bytes.Length, colSize));
                break;
            case ColumnType.Int:
                if (int.TryParse(value, out var i32))
                    WriteInt32LittleEndian(buffer, offset, i32);
                break;
            case ColumnType.TinyInt:
                if (sbyte.TryParse(value, out var i8))
                    buffer[offset] = (byte)i8;
                break;
            case ColumnType.BigInt:
                if (long.TryParse(value, out var i64))
                    WriteInt64LittleEndian(buffer, offset, i64);
                break;
            case ColumnType.Bit:
                buffer[offset] = (byte)(value is "1" or "true" ? 1 : 0);
                break;
            case ColumnType.Decimal:
                if (decimal.TryParse(value, out var dec))
                {
                    var d = (double)dec;
                    var bits = BitConverter.DoubleToInt64Bits(d);
                    WriteInt64LittleEndian(buffer, offset, bits);
                }
                break;
        }
    }

    private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
    {
        for (var i = 0; i < 4; i++)
        {
            buffer[offset + i] = (byte)(value & 0xFF);
            value >>= 8;
        }
    }
}
