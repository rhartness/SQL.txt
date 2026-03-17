namespace SqlTxt.Contracts;

/// <summary>
/// Definition of a table including columns and sharding parameters.
/// </summary>
/// <param name="TableName">Table name.</param>
/// <param name="Columns">Column definitions in order.</param>
/// <param name="MaxShardSize">Maximum bytes per shard before creating new shard. Null = no limit.</param>
public sealed record TableDefinition(
    string TableName,
    IReadOnlyList<ColumnDefinition> Columns,
    long? MaxShardSize = null);
