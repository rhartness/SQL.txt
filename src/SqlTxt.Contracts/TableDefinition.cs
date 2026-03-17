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
    /// Storage width for _RowId (BIGINT max = 20 digits).
    /// </summary>
    public const int RowIdStorageWidth = 20;

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
}
