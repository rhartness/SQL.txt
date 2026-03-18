namespace SqlTxt.Contracts;

/// <summary>
/// Deserializes row data from binary storage format.
/// </summary>
public interface IBinaryRowDeserializer
{
    /// <summary>
    /// Gets the fixed record size in bytes for the given table.
    /// </summary>
    int GetRecordSize(TableDefinition table);

    /// <summary>
    /// Deserializes a binary record into a row.
    /// </summary>
    /// <param name="data">Raw record bytes.</param>
    /// <param name="table">Table definition for column order and widths.</param>
    /// <param name="isActive">True if row is active, false if deleted.</param>
    /// <returns>Deserialized row.</returns>
    RowData Deserialize(ReadOnlySpan<byte> data, TableDefinition table, out bool isActive);
}
