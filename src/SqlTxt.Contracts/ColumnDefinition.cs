namespace SqlTxt.Contracts;

/// <summary>
/// Definition of a single column in a table.
/// </summary>
/// <param name="Name">Column name.</param>
/// <param name="Type">Column data type.</param>
/// <param name="Width">For CHAR: fixed width. For DECIMAL: total digits (precision). Otherwise null.</param>
/// <param name="Scale">For DECIMAL: digits after decimal point. Otherwise null.</param>
/// <param name="IsPrimaryKey">True if column is part of primary key (column-level PK).</param>
/// <param name="IsUnique">True if column has UNIQUE constraint (column-level).</param>
public sealed record ColumnDefinition(
    string Name,
    ColumnType Type,
    int? Width = null,
    int? Scale = null,
    bool IsPrimaryKey = false,
    bool IsUnique = false)
{
    /// <summary>
    /// Gets the storage width in characters for fixed-width serialization.
    /// For VARCHAR, returns max length (used for validation, not padding).
    /// </summary>
    public int StorageWidth => Type switch
    {
        ColumnType.Char => Width ?? 0,
        ColumnType.VarChar => Width ?? 0,
        ColumnType.Int => 11,
        ColumnType.TinyInt => 4,
        ColumnType.BigInt => 20,
        ColumnType.Bit => 1,
        ColumnType.Decimal => (Width ?? 0) + 2, // precision + sign + decimal point
        _ => 0,
    };

    /// <summary>
    /// True if the column uses variable-width storage (VARCHAR).
    /// </summary>
    public bool IsVariableWidth => Type == ColumnType.VarChar;
}
