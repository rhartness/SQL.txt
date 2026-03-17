namespace SqlTxt.Contracts;

/// <summary>
/// Serializes row data to fixed-width format for storage.
/// </summary>
public interface IRowSerializer
{
    /// <summary>
    /// Serializes a row to the storage format (A| or D| prefix + fixed-width fields).
    /// </summary>
    /// <param name="row">Row data.</param>
    /// <param name="table">Table definition for column order and widths.</param>
    /// <param name="isActive">True for active row (A|), false for deleted (D|).</param>
    /// <param name="warnings">Optional list to collect truncation warnings.</param>
    /// <param name="tableName">Table name for warning messages.</param>
    /// <returns>Serialized row string.</returns>
    string Serialize(RowData row, TableDefinition table, bool isActive = true, List<string>? warnings = null, string? tableName = null);
}
