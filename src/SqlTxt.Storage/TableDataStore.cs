using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;

namespace SqlTxt.Storage;

/// <summary>
/// Reads and writes table row data with support for sharding.
/// </summary>
public sealed class TableDataStore : ITableDataStore
{
    private readonly Contracts.IFileSystemAccessor _fs;
    private readonly IRowSerializer _serializer;
    private readonly IRowDeserializer _deserializer;
    private readonly ISchemaStore _schemaStore;

    public TableDataStore(
        Contracts.IFileSystemAccessor fs,
        IRowSerializer serializer,
        IRowDeserializer deserializer,
        ISchemaStore schemaStore)
    {
        _fs = fs;
        _serializer = serializer;
        _deserializer = deserializer;
        _schemaStore = schemaStore;
    }

    public async Task AppendRowAsync(string databasePath, string tableName, RowData row, CancellationToken cancellationToken = default, List<string>? warnings = null)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var line = _serializer.Serialize(row, table, isActive: true, warnings, tableName) + Environment.NewLine;
        var shardIndex = GetAppendShardIndex(databasePath, tableName, table.MaxShardSize, line.Length);
        var dataPath = GetDataFilePath(databasePath, tableName, shardIndex);

        _fs.CreateDirectory(Path.GetDirectoryName(dataPath)!);
        await _fs.AppendAllTextAsync(dataPath, line, cancellationToken).ConfigureAwait(false);
    }

    private int GetAppendShardIndex(string databasePath, string tableName, long? maxShardSize, int newRowBytes)
    {
        if (maxShardSize is null or <= 0)
            return 0;

        var shardIndex = 0;
        while (true)
        {
            var path = GetDataFilePath(databasePath, tableName, shardIndex);
            if (!_fs.FileExists(path))
                return shardIndex;

            var currentSize = _fs.GetFileLength(path);
            if (currentSize + newRowBytes <= maxShardSize.Value)
                return shardIndex;

            shardIndex++;
        }
    }

    public async IAsyncEnumerable<RowData> ReadRowsAsync(string databasePath, string tableName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var basePath = _fs.Combine(databasePath, "Tables", tableName);
        if (!_fs.DirectoryExists(basePath))
            yield break;

        var shardIndex = 0;
        while (true)
        {
            var dataPath = GetDataFilePath(databasePath, tableName, shardIndex);
            if (!_fs.FileExists(dataPath))
                break;

            var lines = await _fs.ReadAllLinesAsync(dataPath, cancellationToken).ConfigureAwait(false);
            var rowNum = 0;
            foreach (var line in lines)
            {
                rowNum++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                RowData row;
                bool isActive;
                try
                {
                    row = _deserializer.Deserialize(line, table, out isActive);
                }
                catch (StorageException ex)
                {
                    throw new StorageException(ex.Message, dataPath, rowNum, null);
                }

                if (isActive)
                    yield return row;
            }

            shardIndex++;
        }
    }

    public async Task<IReadOnlyList<(bool IsActive, RowData Row)>> ReadAllRowsWithStatusAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var result = new List<(bool IsActive, RowData Row)>();
        var basePath = _fs.Combine(databasePath, "Tables", tableName);
        if (!_fs.DirectoryExists(basePath))
            return result;

        var shardIndex = 0;
        while (true)
        {
            var dataPath = GetDataFilePath(databasePath, tableName, shardIndex);
            if (!_fs.FileExists(dataPath))
                break;

            var lines = await _fs.ReadAllLinesAsync(dataPath, cancellationToken).ConfigureAwait(false);
            var rowNum = 0;
            foreach (var line in lines)
            {
                rowNum++;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var row = _deserializer.Deserialize(line, table, out var isActive);
                    result.Add((isActive, row));
                }
                catch (StorageException ex)
                {
                    throw new StorageException(ex.Message, dataPath, rowNum, null);
                }
            }

            shardIndex++;
        }

        return result;
    }

    public async Task WriteAllRowsAsync(string databasePath, string tableName, IReadOnlyList<(bool IsActive, RowData Row)> rows, CancellationToken cancellationToken = default, List<string>? warnings = null)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var lines = rows.Select(r => _serializer.Serialize(r.Row, table, r.IsActive, warnings, tableName)).ToList();
        var content = string.Join(Environment.NewLine, lines) + (lines.Count > 0 ? Environment.NewLine : "");
        var dataPath = GetDataFilePath(databasePath, tableName, shardIndex: 0);

        _fs.CreateDirectory(Path.GetDirectoryName(dataPath)!);
        await _fs.WriteAllTextAsync(dataPath, content, cancellationToken).ConfigureAwait(false);
    }

    private string GetDataFilePath(string databasePath, string tableName, int shardIndex)
    {
        var fileName = shardIndex == 0 ? $"{tableName}.txt" : $"{tableName}_{shardIndex}.txt";
        return _fs.Combine(databasePath, "Tables", tableName, fileName);
    }
}
