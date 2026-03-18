namespace SqlTxt.Contracts;

/// <summary>
/// Shard Table of Contents entry. Format: ShardId|MinRowId|MaxRowId|FilePath|RowCount.
/// </summary>
/// <param name="ShardId">Shard index.</param>
/// <param name="MinRowId">Minimum _RowId in this shard.</param>
/// <param name="MaxRowId">Maximum _RowId in this shard.</param>
/// <param name="FilePath">Data file name (e.g., Table.txt, Table_1.txt).</param>
/// <param name="RowCount">Number of rows in this shard.</param>
public sealed record StocEntry(int ShardId, long MinRowId, long MaxRowId, string FilePath, int RowCount);

/// <summary>
/// Manages Shard Table of Contents (STOC) files for multi-shard tables.
/// File: &lt;TableName&gt;_STOC.txt; format per line: ShardId|MinRowId|MaxRowId|FilePath|RowCount.
/// </summary>
public interface IStocStore
{
    /// <summary>
    /// Writes the STOC file for a table. Overwrites existing.
    /// </summary>
    Task WriteStocAsync(string databasePath, string tableName, IReadOnlyList<StocEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the STOC file. Returns empty list if file does not exist.
    /// </summary>
    Task<IReadOnlyList<StocEntry>> ReadStocAsync(string databasePath, string tableName, CancellationToken cancellationToken = default);
}
