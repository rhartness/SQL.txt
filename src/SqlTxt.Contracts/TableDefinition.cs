namespace SqlTxt.Contracts;

/// <summary>
/// Definition of a table including columns and sharding parameters.
/// </summary>
/// <param name="TableName">Table name.</param>
/// <param name="Columns">Column definitions in order.</param>
/// <param name="MaxShardSize">Maximum bytes per shard before creating new shard. Null = no limit.</param>
/// <param name="PrimaryKeyColumns">Column names for primary key. Empty = no PK.</param>
/// <param name="ForeignKeyDefinitions">Foreign key constraints.</param>
/// <param name="UniqueConstraintColumns">Column names for table-level UNIQUE. Empty = none.</param>
/// <param name="IndexDefinitions">Index definitions (from CREATE INDEX).</param>
public sealed record TableDefinition(
    string TableName,
    IReadOnlyList<ColumnDefinition> Columns,
    long? MaxShardSize = null,
    IReadOnlyList<string>? PrimaryKeyColumns = null,
    IReadOnlyList<ForeignKeyDefinition>? ForeignKeyDefinitions = null,
    IReadOnlyList<string>? UniqueConstraintColumns = null,
    IReadOnlyList<IndexDefinition>? IndexDefinitions = null)
{
    /// <summary>
    /// Internal column name for stable row identification. Not user-visible.
    /// Used for index targeting; survives row rewrites.
    /// </summary>
    public const string RowIdColumnName = "_RowId";

    /// <summary>
    /// Populated in memory after deserialize when MVCC suffix is present on disk.
    /// </summary>
    public const string MvccXminKey = "_MvccXmin";

    /// <summary>
    /// Populated in memory after deserialize when MVCC suffix is present on disk.
    /// </summary>
    public const string MvccXmaxKey = "_MvccXmax";

    /// <summary>
    /// Decimal width for MVCC xid fields in text rows (fixed-width tail).
    /// </summary>
    public const int MvccTextFieldWidth = 20;

    /// <summary>
    /// When set on a row during storage stream transform, that output line is omitted (MVCC vacuum).
    /// </summary>
    public const string VacuumOmitKey = "_VacuumOmit";

    /// <summary>
    /// Storage width for _RowId (BIGINT max = 20 digits).
    /// </summary>
    public const int RowIdStorageWidth = 20;

    /// <summary>
    /// Row format version: 1 = fixed-width only (Phase 1/2), 2 = mixed CHAR + VARCHAR (variable-width).
    /// </summary>
    public const int RowFormatVersionFixedWidth = 1;

    /// <summary>
    /// Row format version for tables with variable-width columns.
    /// </summary>
    public const int RowFormatVersionVariableWidth = 2;

    /// <summary>
    /// Gets primary key columns, or empty if none.
    /// </summary>
    public IReadOnlyList<string> PrimaryKey => PrimaryKeyColumns ?? Array.Empty<string>();

    /// <summary>
    /// Gets foreign key definitions, or empty if none.
    /// </summary>
    public IReadOnlyList<ForeignKeyDefinition> ForeignKeys => ForeignKeyDefinitions ?? Array.Empty<ForeignKeyDefinition>();

    /// <summary>
    /// Gets unique constraint columns, or empty if none.
    /// </summary>
    public IReadOnlyList<string> UniqueColumns => UniqueConstraintColumns ?? Array.Empty<string>();

    /// <summary>
    /// Gets index definitions, or empty if none.
    /// </summary>
    public IReadOnlyList<IndexDefinition> Indexes => IndexDefinitions ?? Array.Empty<IndexDefinition>();

    /// <summary>
    /// True if the table has any variable-width (VARCHAR) column.
    /// </summary>
    public bool HasVariableWidthColumns => Columns.Any(c => c.IsVariableWidth);

    /// <summary>
    /// Row format version for this table: 2 if any VARCHAR column, else 1.
    /// </summary>
    public int RowFormatVersion => HasVariableWidthColumns ? RowFormatVersionVariableWidth : RowFormatVersionFixedWidth;
}
