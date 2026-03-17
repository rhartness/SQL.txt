using System.Text;
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

            var rowNum = 0;
            await foreach (var line in _fs.ReadLinesAsync(dataPath, cancellationToken).ConfigureAwait(false))
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

            var rowNum = 0;
            await foreach (var line in _fs.ReadLinesAsync(dataPath, cancellationToken).ConfigureAwait(false))
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
        var newline = Environment.NewLine;
        var rowSize = lines.Count > 0 ? lines[0].Length + newline.Length : 0;

        var tableDir = Path.GetDirectoryName(GetDataFilePath(databasePath, tableName, 0))!;
        _fs.CreateDirectory(tableDir);

        var maxShardSize = table.MaxShardSize;
        var shardLines = new List<List<string>>();
        var currentShard = new List<string>();

        if (maxShardSize is null or <= 0)
        {
            shardLines.Add(lines);
        }
        else
        {
            var currentSize = 0L;
            foreach (var line in lines)
            {
                var lineBytes = line.Length + newline.Length;
                if (currentShard.Count > 0 && currentSize + lineBytes > maxShardSize.Value)
                {
                    shardLines.Add(currentShard);
                    currentShard = new List<string>();
                    currentSize = 0;
                }
                currentShard.Add(line);
                currentSize += lineBytes;
            }
            if (currentShard.Count > 0)
                shardLines.Add(currentShard);
        }

        for (var i = 0; i < shardLines.Count; i++)
        {
            var shardIndex = i;
            var path = GetDataFilePath(databasePath, tableName, shardIndex);
            var tmpPath = path + ".tmp";

            var sb = new StringBuilder();
            foreach (var line in shardLines[i])
            {
                sb.Append(line).Append(newline);
            }
            if (shardLines[i].Count > 0)
                sb.Append(newline);

            await _fs.WriteAllTextAsync(tmpPath, sb.ToString(), cancellationToken).ConfigureAwait(false);
            _fs.MoveFile(tmpPath, path);
        }

        var existingShardIndex = shardLines.Count;
        while (true)
        {
            var oldPath = GetDataFilePath(databasePath, tableName, existingShardIndex);
            if (!_fs.FileExists(oldPath))
                break;
            _fs.DeleteFile(oldPath);
            existingShardIndex++;
        }
    }

    public async Task<(int TotalRows, int ActiveRows, int DeletedRows)> StreamTransformRowsAsync(
        string databasePath,
        string tableName,
        Func<(bool IsActive, RowData Row), (bool IsActive, RowData Row)> transform,
        CancellationToken cancellationToken = default,
        List<string>? warnings = null)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var tableDir = Path.GetDirectoryName(GetDataFilePath(databasePath, tableName, 0))!;
        _fs.CreateDirectory(tableDir);

        var newline = Environment.NewLine;
        var maxShardSize = table.MaxShardSize;
        var currentShardLines = new List<string>();
        var currentShardSize = 0L;
        var outputShardIndex = 0;
        var total = 0;
        var active = 0;

        async Task FlushShardAsync()
        {
            if (currentShardLines.Count == 0)
                return;

            var path = GetDataFilePath(databasePath, tableName, outputShardIndex);
            var tmpPath = path + ".tmp";

            var sb = new StringBuilder();
            foreach (var line in currentShardLines)
            {
                sb.Append(line).Append(newline);
            }
            sb.Append(newline);

            await _fs.WriteAllTextAsync(tmpPath, sb.ToString(), cancellationToken).ConfigureAwait(false);
            _fs.MoveFile(tmpPath, path);

            currentShardLines.Clear();
            currentShardSize = 0;
            outputShardIndex++;
        }

        var shardIndex = 0;
        while (true)
        {
            var dataPath = GetDataFilePath(databasePath, tableName, shardIndex);
            if (!_fs.FileExists(dataPath))
                break;

            var rowNum = 0;
            await foreach (var line in _fs.ReadLinesAsync(dataPath, cancellationToken).ConfigureAwait(false))
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

                var transformed = transform((isActive, row));
                total++;
                if (transformed.IsActive)
                    active++;

                var serialized = _serializer.Serialize(transformed.Row, table, transformed.IsActive, warnings, tableName);
                var lineBytes = serialized.Length + newline.Length;

                if (maxShardSize is > 0 && currentShardLines.Count > 0 && currentShardSize + lineBytes > maxShardSize.Value)
                    await FlushShardAsync().ConfigureAwait(false);

                currentShardLines.Add(serialized);
                currentShardSize += lineBytes;
            }

            shardIndex++;
        }

        await FlushShardAsync().ConfigureAwait(false);

        var existingShardIndex = outputShardIndex;
        while (true)
        {
            var oldPath = GetDataFilePath(databasePath, tableName, existingShardIndex);
            if (!_fs.FileExists(oldPath))
                break;
            _fs.DeleteFile(oldPath);
            existingShardIndex++;
        }

        return (total, active, total - active);
    }

    private string GetDataFilePath(string databasePath, string tableName, int shardIndex)
    {
        var fileName = shardIndex == 0 ? $"{tableName}.txt" : $"{tableName}_{shardIndex}.txt";
        return _fs.Combine(databasePath, "Tables", tableName, fileName);
    }
}
