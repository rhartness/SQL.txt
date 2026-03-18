using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Manages Shard Table of Contents files. Format per line: ShardId|MinRowId|MaxRowId|FilePath|RowCount.
/// </summary>
public sealed class StocStore : IStocStore
{
    private readonly Contracts.IFileSystemAccessor _fs;

    public StocStore(Contracts.IFileSystemAccessor fs) => _fs = fs;

    public async Task WriteStocAsync(string databasePath, string tableName, IReadOnlyList<StocEntry> entries, CancellationToken cancellationToken = default)
    {
        var path = GetStocPath(databasePath, tableName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);

        var lines = entries.Select(e => $"{e.ShardId}|{e.MinRowId}|{e.MaxRowId}|{e.FilePath}|{e.RowCount}");
        var content = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        await _fs.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StocEntry>> ReadStocAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var path = GetStocPath(databasePath, tableName);
        if (!_fs.FileExists(path))
            return Array.Empty<StocEntry>();

        var result = new List<StocEntry>();
        await foreach (var line in _fs.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('|');
            if (parts.Length < 5)
                continue;

            if (!int.TryParse(parts[0].Trim(), out var shardId) ||
                !long.TryParse(parts[1].Trim(), out var minRowId) ||
                !long.TryParse(parts[2].Trim(), out var maxRowId) ||
                !int.TryParse(parts[4].Trim(), out var rowCount))
                continue;

            result.Add(new StocEntry(shardId, minRowId, maxRowId, parts[3].Trim(), rowCount));
        }

        return result;
    }

    private string GetStocPath(string databasePath, string tableName) =>
        _fs.Combine(databasePath, "Tables", tableName, $"{tableName}_STOC.txt");

}
