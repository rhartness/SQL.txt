using SqlTxt.Contracts;

namespace SqlTxt.Storage;

/// <summary>
/// Persists table metadata (row counts, last updated).
/// </summary>
public sealed class MetadataStore : IMetadataStore
{
    private const string MetaFileName = "table.meta.txt";

    private readonly Contracts.IFileSystemAccessor _fs;

    public MetadataStore(Contracts.IFileSystemAccessor fs) => _fs = fs;

    public async Task UpdateMetadataAsync(string databasePath, string tableName, long rowCount, long activeRowCount, long deletedRowCount, CancellationToken cancellationToken = default)
    {
        var content = $@"TABLE: {tableName}
ROW_COUNT: {rowCount}
ACTIVE_ROW_COUNT: {activeRowCount}
DELETED_ROW_COUNT: {deletedRowCount}
LAST_UPDATED_UTC: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}
";

        var path = _fs.Combine(databasePath, "Tables", tableName, MetaFileName);
        _fs.CreateDirectory(Path.GetDirectoryName(path)!);
        await _fs.WriteAllTextAsync(path, content, cancellationToken).ConfigureAwait(false);
    }

    public async Task<(long RowCount, long ActiveRowCount, long DeletedRowCount)> ReadMetadataAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var path = _fs.Combine(databasePath, "Tables", tableName, MetaFileName);
        if (!_fs.FileExists(path))
            return (0, 0, 0);

        var content = await _fs.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        long rowCount = 0, activeRowCount = 0, deletedRowCount = 0;

        foreach (var line in content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.StartsWith("ROW_COUNT:"))
                long.TryParse(line["ROW_COUNT:".Length..].Trim(), out rowCount);
            else if (line.StartsWith("ACTIVE_ROW_COUNT:"))
                long.TryParse(line["ACTIVE_ROW_COUNT:".Length..].Trim(), out activeRowCount);
            else if (line.StartsWith("DELETED_ROW_COUNT:"))
                long.TryParse(line["DELETED_ROW_COUNT:".Length..].Trim(), out deletedRowCount);
        }

        return (rowCount, activeRowCount, deletedRowCount);
    }
}
