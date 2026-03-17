namespace SqlTxt.Contracts;

/// <summary>
/// Deserializes row data from fixed-width storage format.
/// </summary>
public interface IRowDeserializer
{
    /// <summary>
    /// Deserializes a line from the data file into a row.
    /// </summary>
    /// <param name="line">Raw line from file.</param>
    /// <param name="table">Table definition for column order and widths.</param>
    /// <param name="isActive">True if row is active (A|), false if deleted (D|).</param>
    /// <returns>Deserialized row.</returns>
    RowData Deserialize(string line, TableDefinition table, out bool isActive);
}
