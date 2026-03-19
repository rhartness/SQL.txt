namespace SqlTxt.Contracts;

/// <summary>
/// Serializes row data to binary format for storage.
/// </summary>
public interface IBinaryRowSerializer
{
    /// <summary>
    /// Gets the fixed record size in bytes for the given table.
    /// </summary>
    int GetRecordSize(TableDefinition table);

    /// <summary>
    /// Record size including optional 16-byte MVCC trailer (two little-endian int64).
    /// </summary>
    int GetRecordSize(TableDefinition table, bool includeMvcc);

    /// <summary>
    /// Serializes a row to binary format.
    /// </summary>
    /// <param name="row">Row data.</param>
    /// <param name="table">Table definition for column order and widths.</param>
    /// <param name="isActive">True for active row, false for deleted.</param>
    /// <param name="warnings">Optional list to collect truncation warnings.</param>
    /// <param name="tableName">Table name for warning messages.</param>
    /// <param name="mvcc">Optional MVCC trailer; null = no trailer (legacy size).</param>
    /// <returns>Serialized row bytes (exactly <see cref="GetRecordSize(TableDefinition, bool)"/> bytes).</returns>
    byte[] Serialize(RowData row, TableDefinition table, bool isActive = true, List<string>? warnings = null, string? tableName = null, MvccRowVersions? mvcc = null);
}
