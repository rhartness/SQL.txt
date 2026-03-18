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
    private readonly IBinaryRowSerializer _binarySerializer;
    private readonly IBinaryRowDeserializer _binaryDeserializer;
    private readonly ISchemaStore _schemaStore;
    private readonly IRowIdSequenceStore _rowIdStore;
    private readonly IStocStore _stocStore;
    private readonly IIndexStore? _indexStore;
    private readonly IStorageBackendResolver? _backendResolver;

    public TableDataStore(
        Contracts.IFileSystemAccessor fs,
        IRowSerializer serializer,
        IRowDeserializer deserializer,
        ISchemaStore schemaStore,
        IRowIdSequenceStore? rowIdStore = null,
        IStocStore? stocStore = null,
        IIndexStore? indexStore = null,
        IStorageBackendResolver? backendResolver = null,
        IBinaryRowSerializer? binarySerializer = null,
        IBinaryRowDeserializer? binaryDeserializer = null)
    {
        _fs = fs;
        _serializer = serializer;
        _deserializer = deserializer;
        _binarySerializer = binarySerializer ?? new BinaryRowSerializer();
        _binaryDeserializer = binaryDeserializer ?? new BinaryRowDeserializer();
        _schemaStore = schemaStore;
        _rowIdStore = rowIdStore ?? new RowIdSequenceStore(fs);
        _stocStore = stocStore ?? new StocStore(fs);
        _indexStore = indexStore;
        _backendResolver = backendResolver;
    }

    public async Task<(int ShardIndex, long RowId)> AppendRowAsync(string databasePath, string tableName, RowData row, CancellationToken cancellationToken = default, List<string>? warnings = null)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var backend = _backendResolver != null
            ? await _backendResolver.ResolveAsync(databasePath, cancellationToken).ConfigureAwait(false)
            : null;
        var useBinary = backend is { IsTextBackend: false };

        var rowToSerialize = row;
        long rowId;
        var usesRowId = table.PrimaryKey.Count > 0 || table.ForeignKeys.Count > 0 || table.UniqueColumns.Count > 0;
        if (usesRowId && row.GetValue(TableDefinition.RowIdColumnName) == null)
        {
            rowId = await _rowIdStore.GetNextAndIncrementAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false);
            var dict = new Dictionary<string, string>(row.Values, StringComparer.OrdinalIgnoreCase)
            {
                [TableDefinition.RowIdColumnName] = rowId.ToString()
            };
            rowToSerialize = new RowData(dict);
        }
        else
        {
            var rowIdStr = rowToSerialize.GetValue(TableDefinition.RowIdColumnName);
            rowId = long.TryParse(rowIdStr, out var rid) ? rid : 0;
        }

        int newRowBytes;
        if (useBinary)
        {
            newRowBytes = _binarySerializer.GetRecordSize(table);
        }
        else
        {
            var line = _serializer.Serialize(rowToSerialize, table, isActive: true, warnings, tableName) + Environment.NewLine;
            newRowBytes = line.Length;
        }

        var ext = backend?.DataFileExtension ?? ".txt";
        if (table.MaxShardSize is > 0)
        {
            var path0 = GetDataFilePath(databasePath, tableName, 0, ext);
            var path1 = GetDataFilePath(databasePath, tableName, 1, ext);
            if (_fs.FileExists(path0) && !_fs.FileExists(path1) && _fs.GetFileLength(path0) + newRowBytes > table.MaxShardSize.Value)
                await SplitShardAsync(databasePath, tableName, table, 0, ext, useBinary, cancellationToken).ConfigureAwait(false);
        }

        var shardIndex = GetAppendShardIndex(databasePath, tableName, table.MaxShardSize, newRowBytes, ext);
        var dataPath = GetDataFilePath(databasePath, tableName, shardIndex, ext);

        _fs.CreateDirectory(Path.GetDirectoryName(dataPath)!);
        if (useBinary)
        {
            var bytes = _binarySerializer.Serialize(rowToSerialize, table, isActive: true, warnings, tableName);
            await _fs.AppendAllBytesAsync(dataPath, bytes, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var line = _serializer.Serialize(rowToSerialize, table, isActive: true, warnings, tableName) + Environment.NewLine;
            await _fs.AppendAllTextAsync(dataPath, line, cancellationToken).ConfigureAwait(false);
        }

        if (shardIndex >= 1)
            await BuildAndWriteStocAsync(databasePath, tableName, table, cancellationToken, ext).ConfigureAwait(false);

        return (shardIndex, rowId);
    }

    private async Task SplitShardAsync(string databasePath, string tableName, TableDefinition table, int shardToSplit, string ext = ".txt", bool useBinary = false, CancellationToken cancellationToken = default)
    {
        var dataPath = GetDataFilePath(databasePath, tableName, shardToSplit, ext);
        if (!_fs.FileExists(dataPath))
            return;

        var rows = new List<(bool IsActive, RowData Row, string? Serialized, byte[]? SerializedBytes)>();
        if (useBinary)
        {
            var recordSize = _binaryDeserializer.GetRecordSize(table);
            var bytes = await _fs.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i + recordSize <= bytes.Length; i += recordSize)
            {
                var slice = new ReadOnlySpan<byte>(bytes, i, recordSize);
                var row = _binaryDeserializer.Deserialize(slice, table, out var isActive);
                var serializedBytes = _binarySerializer.Serialize(row, table, isActive, null, tableName);
                rows.Add((isActive, row, null, serializedBytes));
            }
        }
        else
        {
            await foreach (var line in _fs.ReadLinesAsync(dataPath, cancellationToken).ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var row = _deserializer.Deserialize(line, table, out var isActive);
                var serialized = _serializer.Serialize(row, table, isActive, null, tableName);
                rows.Add((isActive, row, serialized, null));
            }
        }

        if (rows.Count < 2)
            return;

        var half = rows.Count / 2;
        var firstHalf = rows.Take(half).ToList();
        var secondHalf = rows.Skip(half).ToList();
        var newline = Environment.NewLine;

        var path0 = GetDataFilePath(databasePath, tableName, shardToSplit, ext);
        var path1 = GetDataFilePath(databasePath, tableName, shardToSplit + 1, ext);
        var tmp0 = path0 + ".split.tmp";
        var tmp1 = path1 + ".split.tmp";

        _fs.CreateDirectory(Path.GetDirectoryName(path0)!);

        if (useBinary)
        {
            var all0 = new List<byte>();
            foreach (var (_, _, _, bytes) in firstHalf)
            {
                if (bytes != null)
                    all0.AddRange(bytes);
            }
            await _fs.WriteAllBytesAsync(tmp0, all0.ToArray(), cancellationToken).ConfigureAwait(false);

            var all1 = new List<byte>();
            foreach (var (_, _, _, bytes) in secondHalf)
            {
                if (bytes != null)
                    all1.AddRange(bytes);
            }
            await _fs.WriteAllBytesAsync(tmp1, all1.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var sb0 = new StringBuilder();
            foreach (var (_, _, s, _) in firstHalf)
                sb0.Append(s).Append(newline);
            sb0.Append(newline);
            await _fs.WriteAllTextAsync(tmp0, sb0.ToString(), cancellationToken).ConfigureAwait(false);

            var sb1 = new StringBuilder();
            foreach (var (_, _, s, _) in secondHalf)
                sb1.Append(s).Append(newline);
            sb1.Append(newline);
            await _fs.WriteAllTextAsync(tmp1, sb1.ToString(), cancellationToken).ConfigureAwait(false);
        }

        _fs.MoveFile(tmp0, path0);
        _fs.MoveFile(tmp1, path1);

        if (_indexStore != null)
        {
            foreach (var (_, row, _, _) in secondHalf)
            {
                var rowIdStr = row.GetValue(TableDefinition.RowIdColumnName);
                if (string.IsNullOrEmpty(rowIdStr) || !long.TryParse(rowIdStr, out var rowId))
                    continue;

                if (table.PrimaryKey.Count > 0)
                {
                    var pkKey = IndexStore.FormatCompositeKeyFromRow(row, table.PrimaryKey);
                    await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, tableName, "_PK", pkKey, rowId, cancellationToken).ConfigureAwait(false);
                    await _indexStore.AddIndexEntryAsync(databasePath, tableName, "_PK", pkKey, rowId, shardToSplit + 1, cancellationToken).ConfigureAwait(false);
                }
                foreach (var fk in table.ForeignKeys)
                {
                    var fkVal = row.GetValue(fk.ColumnName) ?? "";
                    var idxName = $"_FK_{fk.ReferencedTable}";
                    await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, tableName, idxName, fkVal, rowId, cancellationToken).ConfigureAwait(false);
                    await _indexStore.AddIndexEntryAsync(databasePath, tableName, idxName, fkVal, rowId, shardToSplit + 1, cancellationToken).ConfigureAwait(false);
                }
                if (table.UniqueColumns.Count > 0)
                {
                    var uqKey = IndexStore.FormatCompositeKeyFromRow(row, table.UniqueColumns);
                    var uqName = "_UQ_" + string.Join("_", table.UniqueColumns);
                    await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, tableName, uqName, uqKey, rowId, cancellationToken).ConfigureAwait(false);
                    await _indexStore.AddIndexEntryAsync(databasePath, tableName, uqName, uqKey, rowId, shardToSplit + 1, cancellationToken).ConfigureAwait(false);
                }
                foreach (var idx in table.Indexes)
                {
                    var idxFileName = GetIndexFileNameForDefinition(table, idx);
                    var key = IndexStore.FormatCompositeKeyFromRow(row, idx.ColumnNames);
                    await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, tableName, idxFileName, key, rowId, cancellationToken).ConfigureAwait(false);
                    await _indexStore.AddIndexEntryAsync(databasePath, tableName, idxFileName, key, rowId, shardToSplit + 1, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await BuildAndWriteStocAsync(databasePath, tableName, table, cancellationToken, ext).ConfigureAwait(false);
    }

    private static string GetIndexFileNameForDefinition(TableDefinition table, IndexDefinition idx)
    {
        var sameCols = (table.Indexes ?? Array.Empty<IndexDefinition>()).Where(i => i.ColumnNames.SequenceEqual(idx.ColumnNames, StringComparer.OrdinalIgnoreCase)).ToList();
        var pos = sameCols.IndexOf(idx);
        return "_INX_" + string.Join("_", idx.ColumnNames) + "_" + (pos >= 0 ? pos : 0);
    }

    private int GetAppendShardIndex(string databasePath, string tableName, long? maxShardSize, int newRowBytes, string ext = ".txt")
    {
        if (maxShardSize is null or <= 0)
            return 0;

        var shardIndex = 0;
        while (true)
        {
            var path = GetDataFilePath(databasePath, tableName, shardIndex, ext);
            if (!_fs.FileExists(path))
                return shardIndex;

            var currentSize = _fs.GetFileLength(path);
            if (currentSize + newRowBytes <= maxShardSize.Value)
                return shardIndex;

            shardIndex++;
        }
    }

    private async Task BuildAndWriteStocAsync(string databasePath, string tableName, TableDefinition table, CancellationToken cancellationToken, string ext = ".txt")
    {
        var entries = new List<StocEntry>();
        var shardIndex = 0;
        var useBinary = ext == ".bin";
        var recordSize = useBinary ? _binaryDeserializer.GetRecordSize(table) : 0;

        while (true)
        {
            var dataPath = GetDataFilePath(databasePath, tableName, shardIndex, ext);
            if (!_fs.FileExists(dataPath))
                break;

            var fileName = Path.GetFileName(dataPath);
            long minRowId = long.MaxValue, maxRowId = long.MinValue;
            var rowCount = 0;

            if (useBinary)
            {
                var bytes = await _fs.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
                for (var i = 0; i + recordSize <= bytes.Length; i += recordSize)
                {
                    var slice = new ReadOnlySpan<byte>(bytes, i, recordSize);
                    var row = _binaryDeserializer.Deserialize(slice, table, out _);
                    var rowIdStr = row.GetValue(TableDefinition.RowIdColumnName);
                    if (!string.IsNullOrEmpty(rowIdStr) && long.TryParse(rowIdStr, out var rowId))
                    {
                        if (rowId < minRowId) minRowId = rowId;
                        if (rowId > maxRowId) maxRowId = rowId;
                        rowCount++;
                    }
                }
            }
            else
            {
                await foreach (var line in _fs.ReadLinesAsync(dataPath, cancellationToken).ConfigureAwait(false))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    var row = _deserializer.Deserialize(line, table, out _);
                    var rowIdStr = row.GetValue(TableDefinition.RowIdColumnName);
                    if (!string.IsNullOrEmpty(rowIdStr) && long.TryParse(rowIdStr, out var rowId))
                    {
                        if (rowId < minRowId) minRowId = rowId;
                        if (rowId > maxRowId) maxRowId = rowId;
                        rowCount++;
                    }
                }
            }

            if (rowCount > 0)
                entries.Add(new StocEntry(shardIndex, minRowId, maxRowId, fileName, rowCount));
            else
                entries.Add(new StocEntry(shardIndex, 0, 0, fileName, 0));

            shardIndex++;
        }

        if (entries.Count >= 2)
            await _stocStore.WriteStocAsync(databasePath, tableName, entries, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<RowData> ReadRowsAsync(string databasePath, string tableName, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var ext = ".txt";
        if (_backendResolver != null)
        {
            var backend = await _backendResolver.ResolveAsync(databasePath, cancellationToken).ConfigureAwait(false);
            ext = backend.DataFileExtension;
        }

        var basePath = _fs.Combine(databasePath, "Tables", tableName);
        if (!_fs.DirectoryExists(basePath))
            yield break;

        var shardPaths = new List<string>();
        var shardIndex = 0;
        while (true)
        {
            var dataPath = GetDataFilePath(databasePath, tableName, shardIndex, ext);
            if (!_fs.FileExists(dataPath))
                break;
            shardPaths.Add(dataPath);
            shardIndex++;
        }

        if (shardPaths.Count == 0)
            yield break;

        var useBinary = ext == ".bin";
        var shardTasks = shardPaths.Select(path => ReadShardRowsAsync(path, table, useBinary, cancellationToken)).ToList();
        var shardResults = await Task.WhenAll(shardTasks).ConfigureAwait(false);

        foreach (var rows in shardResults)
        {
            foreach (var row in rows)
            {
                yield return row;
            }
        }
    }

    private async Task<List<RowData>> ReadShardRowsAsync(string dataPath, TableDefinition table, bool useBinary, CancellationToken cancellationToken)
    {
        var result = new List<RowData>();
        var rowNum = 0;

        if (useBinary)
        {
            var recordSize = _binaryDeserializer.GetRecordSize(table);
            var bytes = await _fs.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i + recordSize <= bytes.Length; i += recordSize)
            {
                rowNum++;
                try
                {
                    var slice = new ReadOnlySpan<byte>(bytes, i, recordSize);
                    var row = _binaryDeserializer.Deserialize(slice, table, out var isActive);
                    if (isActive)
                        result.Add(row);
                }
                catch (StorageException ex)
                {
                    throw new StorageException(ex.Message, dataPath, rowNum, null);
                }
            }
        }
        else
        {
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
                    result.Add(row);
            }
        }
        return result;
    }

    public async Task<IReadOnlyList<(bool IsActive, RowData Row)>> ReadAllRowsWithStatusAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var ext = ".txt";
        if (_backendResolver != null)
        {
            var backend = await _backendResolver.ResolveAsync(databasePath, cancellationToken).ConfigureAwait(false);
            ext = backend.DataFileExtension;
        }

        var result = new List<(bool IsActive, RowData Row)>();
        var basePath = _fs.Combine(databasePath, "Tables", tableName);
        if (!_fs.DirectoryExists(basePath))
            return result;

        var useBinary = ext == ".bin";
        var recordSize = useBinary ? _binaryDeserializer.GetRecordSize(table) : 0;
        var shardIndex = 0;

        while (true)
        {
            var dataPath = GetDataFilePath(databasePath, tableName, shardIndex, ext);
            if (!_fs.FileExists(dataPath))
                break;

            var rowNum = 0;
            if (useBinary)
            {
                var bytes = await _fs.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
                for (var i = 0; i + recordSize <= bytes.Length; i += recordSize)
                {
                    rowNum++;
                    try
                    {
                        var slice = new ReadOnlySpan<byte>(bytes, i, recordSize);
                        var row = _binaryDeserializer.Deserialize(slice, table, out var isActive);
                        result.Add((isActive, row));
                    }
                    catch (StorageException ex)
                    {
                        throw new StorageException(ex.Message, dataPath, rowNum, null);
                    }
                }
            }
            else
            {
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
            }

            shardIndex++;
        }

        return result;
    }

    public async Task WriteAllRowsAsync(string databasePath, string tableName, IReadOnlyList<(bool IsActive, RowData Row)> rows, CancellationToken cancellationToken = default, List<string>? warnings = null)
    {
        var table = await _schemaStore.ReadSchemaAsync(databasePath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        var ext = ".txt";
        if (_backendResolver != null)
        {
            var backend = await _backendResolver.ResolveAsync(databasePath, cancellationToken).ConfigureAwait(false);
            ext = backend.DataFileExtension;
        }

        var tableDir = Path.GetDirectoryName(GetDataFilePath(databasePath, tableName, 0, ext))!;
        _fs.CreateDirectory(tableDir);

        var useBinary = ext == ".bin";
        var recordSize = useBinary ? _binarySerializer.GetRecordSize(table) : 0;
        var newline = Environment.NewLine;

        var maxShardSize = table.MaxShardSize;
        var shardLines = new List<List<string>>();
        var shardBytes = new List<List<byte[]>>();
        var currentShard = new List<string>();
        var currentShardBytes = new List<byte[]>();
        var currentSize = 0L;

        if (useBinary)
        {
            foreach (var r in rows)
            {
                var bytes = _binarySerializer.Serialize(r.Row, table, r.IsActive, warnings, tableName);
                if (maxShardSize is > 0 && currentShardBytes.Count > 0 && currentSize + bytes.Length > maxShardSize.Value)
                {
                    shardBytes.Add(currentShardBytes);
                    currentShardBytes = new List<byte[]>();
                    currentSize = 0;
                }
                currentShardBytes.Add(bytes);
                currentSize += bytes.Length;
            }
            if (currentShardBytes.Count > 0)
                shardBytes.Add(currentShardBytes);
        }
        else
        {
            var lines = rows.Select(r => _serializer.Serialize(r.Row, table, r.IsActive, warnings, tableName)).ToList();
            foreach (var line in lines)
            {
                var lineBytes = line.Length + newline.Length;
                if (maxShardSize is > 0 && currentShard.Count > 0 && currentSize + lineBytes > maxShardSize.Value)
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

        if (useBinary)
        {
            for (var i = 0; i < shardBytes.Count; i++)
            {
                var path = GetDataFilePath(databasePath, tableName, i, ext);
                var tmpPath = path + ".tmp";
                var allBytes = shardBytes[i].SelectMany(b => b).ToArray();
                await _fs.WriteAllBytesAsync(tmpPath, allBytes, cancellationToken).ConfigureAwait(false);
                _fs.MoveFile(tmpPath, path);
            }
        }
        else
        {
            for (var i = 0; i < shardLines.Count; i++)
            {
                var path = GetDataFilePath(databasePath, tableName, i, ext);
                var tmpPath = path + ".tmp";
                var sb = new StringBuilder();
                foreach (var line in shardLines[i])
                    sb.Append(line).Append(newline);
                if (shardLines[i].Count > 0)
                    sb.Append(newline);
                await _fs.WriteAllTextAsync(tmpPath, sb.ToString(), cancellationToken).ConfigureAwait(false);
                _fs.MoveFile(tmpPath, path);
            }
        }

        var existingShardIndex = useBinary ? shardBytes.Count : shardLines.Count;
        while (true)
        {
            var oldPath = GetDataFilePath(databasePath, tableName, existingShardIndex, ext);
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

        var ext = ".txt";
        if (_backendResolver != null)
        {
            var backend = await _backendResolver.ResolveAsync(databasePath, cancellationToken).ConfigureAwait(false);
            ext = backend.DataFileExtension;
        }

        var useBinary = ext == ".bin";
        var recordSize = useBinary ? _binaryDeserializer.GetRecordSize(table) : 0;
        var tableDir = Path.GetDirectoryName(GetDataFilePath(databasePath, tableName, 0, ext))!;
        _fs.CreateDirectory(tableDir);

        var newline = Environment.NewLine;
        var maxShardSize = table.MaxShardSize;
        var currentShardLines = new List<string>();
        var currentShardBytes = new List<byte[]>();
        var currentShardSize = 0L;
        var outputShardIndex = 0;
        var total = 0;
        var active = 0;

        async Task FlushShardAsync()
        {
            if (useBinary)
            {
                if (currentShardBytes.Count == 0)
                    return;
                var path = GetDataFilePath(databasePath, tableName, outputShardIndex, ext);
                var tmpPath = path + ".tmp";
                var allBytes = currentShardBytes.SelectMany(b => b).ToArray();
                await _fs.WriteAllBytesAsync(tmpPath, allBytes, cancellationToken).ConfigureAwait(false);
                _fs.MoveFile(tmpPath, path);
                currentShardBytes.Clear();
            }
            else
            {
                if (currentShardLines.Count == 0)
                    return;
                var path = GetDataFilePath(databasePath, tableName, outputShardIndex, ext);
                var tmpPath = path + ".tmp";
                var sb = new StringBuilder();
                foreach (var line in currentShardLines)
                    sb.Append(line).Append(newline);
                sb.Append(newline);
                await _fs.WriteAllTextAsync(tmpPath, sb.ToString(), cancellationToken).ConfigureAwait(false);
                _fs.MoveFile(tmpPath, path);
                currentShardLines.Clear();
            }
            currentShardSize = 0;
            outputShardIndex++;
        }

        var shardIndex = 0;
        while (true)
        {
            var dataPath = GetDataFilePath(databasePath, tableName, shardIndex, ext);
            if (!_fs.FileExists(dataPath))
                break;

            var rowNum = 0;
            if (useBinary)
            {
                var bytes = await _fs.ReadAllBytesAsync(dataPath, cancellationToken).ConfigureAwait(false);
                for (var i = 0; i + recordSize <= bytes.Length; i += recordSize)
                {
                    rowNum++;
                    RowData row;
                    bool isActive;
                    try
                    {
                        var slice = new ReadOnlySpan<byte>(bytes, i, recordSize);
                        row = _binaryDeserializer.Deserialize(slice, table, out isActive);
                    }
                    catch (StorageException ex)
                    {
                        throw new StorageException(ex.Message, dataPath, rowNum, null);
                    }

                    var transformed = transform((isActive, row));
                    total++;
                    if (transformed.IsActive)
                        active++;

                    var serializedBytes = _binarySerializer.Serialize(transformed.Row, table, transformed.IsActive, warnings, tableName);
                    if (maxShardSize is > 0 && currentShardBytes.Count > 0 && currentShardSize + serializedBytes.Length > maxShardSize.Value)
                        await FlushShardAsync().ConfigureAwait(false);

                    currentShardBytes.Add(serializedBytes);
                    currentShardSize += serializedBytes.Length;
                }
            }
            else
            {
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
            }

            shardIndex++;
        }

        await FlushShardAsync().ConfigureAwait(false);

        var existingShardIndex = outputShardIndex;
        while (true)
        {
            var oldPath = GetDataFilePath(databasePath, tableName, existingShardIndex, ext);
            if (!_fs.FileExists(oldPath))
                break;
            _fs.DeleteFile(oldPath);
            existingShardIndex++;
        }

        if (outputShardIndex >= 2)
            await BuildAndWriteStocAsync(databasePath, tableName, table, cancellationToken, ext).ConfigureAwait(false);
        else if (outputShardIndex == 1 && _fs.FileExists(GetStocPath(databasePath, tableName)))
            _fs.DeleteFile(GetStocPath(databasePath, tableName));

        return (total, active, total - active);
    }

    private string GetStocPath(string databasePath, string tableName) =>
        _fs.Combine(databasePath, "Tables", tableName, $"{tableName}_STOC.txt");

    private string GetDataFilePath(string databasePath, string tableName, int shardIndex, string ext = ".txt")
    {
        var baseName = shardIndex == 0 ? tableName : $"{tableName}_{shardIndex}";
        var fileName = ext.StartsWith(".") ? baseName + ext : baseName + "." + ext.TrimStart('.');
        return _fs.Combine(databasePath, "Tables", tableName, fileName);
    }
}
