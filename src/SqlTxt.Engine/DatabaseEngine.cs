using SqlTxt.Contracts;
using SqlTxt.Contracts.Commands;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Parser;
using SqlTxt.Storage;

namespace SqlTxt.Engine;

/// <summary>
/// Main database engine implementation.
/// </summary>
public sealed partial class DatabaseEngine : IDatabaseEngine
{
    private readonly ICommandParser? _parser;
    private readonly SqlCommandParser _defaultParser;
    private readonly object _defaultParserLock = new();
    private readonly IDatabaseLockManager _lockManager;
    private readonly IMvccXidStore _mvccXidStore;
    private readonly IRowLockManager _rowLockManager;
    private readonly Contracts.IFileSystemAccessor _fs;
    private readonly DatabaseCreator _dbCreator;
    private readonly ISchemaStore _schemaStore;
    private readonly TableDataStore _tableDataStore;
    private readonly IMetadataStore _metadataStore;
    private readonly IIndexStore _indexStore;
    private readonly IRowIdSequenceStore _rowIdStore;

    /// <summary>
    /// Parses SQL. When no custom <see cref="ICommandParser"/> is supplied, uses a shared <see cref="SqlCommandParser"/> under a lock (parser is not thread-safe across concurrent calls).
    /// </summary>
    private object ParseSql(string sql)
    {
        if (_parser is not null)
            return _parser.Parse(sql);
        lock (_defaultParserLock)
            return _defaultParser.Parse(sql);
    }

    public DatabaseEngine(
        ICommandParser? parser = null,
        IDatabaseLockManager? lockManager = null,
        Contracts.IFileSystemAccessor? fs = null,
        IIndexStore? indexStore = null,
        IRowIdSequenceStore? rowIdStore = null,
        IMvccXidStore? mvccXidStore = null,
        IRowLockManager? rowLockManager = null,
        bool useFileSystemCache = true,
        long maxCachedBytes = 67_108_864)
    {
        _parser = parser;
        _defaultParser = new SqlCommandParser();
        _lockManager = lockManager ?? new DatabaseLockManager();
        var baseFs = fs ?? new FileSystemAccessor();
        _fs = useFileSystemCache && fs is null ? new CachingFileSystemAccessor(baseFs, maxCachedBytes) : baseFs;
        _indexStore = indexStore ?? new CachingIndexStore(new IndexStore(_fs), _fs);
        _rowIdStore = rowIdStore ?? new RowIdSequenceStore(_fs);
        _mvccXidStore = mvccXidStore ?? new MvccXidStore(_fs);
        _rowLockManager = rowLockManager ?? new RowLockManager();

        var serializer = new FormatAwareRowSerializer();
        var deserializer = new FormatAwareRowDeserializer();
        var schemaStore = new SchemaStore(_fs);
        _schemaStore = new CachingSchemaStore(schemaStore);
        _metadataStore = new CachingMetadataStore(new MetadataStore(_fs));
        _dbCreator = new DatabaseCreator(_fs);
        var backendResolver = new CachingStorageBackendResolver(new StorageBackendResolver(_dbCreator));
        _tableDataStore = new TableDataStore(_fs, serializer, deserializer, _schemaStore, _rowIdStore, stocStore: null, indexStore: _indexStore, backendResolver: backendResolver);
    }

    /// <summary>
    /// Loads a filesystem-backed database into memory and returns an engine configured for in-memory operation.
    /// Call FlushToDiskAsync when done to persist changes back to disk.
    /// </summary>
    /// <param name="databasePath">Full path to the database directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Engine, engine path (virtual root), and flush callback.</returns>
    public static async Task<(IDatabaseEngine Engine, string EnginePath, Func<Task> FlushToDiskAsync)> LoadIntoMemoryAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var (memory, virtualRoot) = await DatabaseLoader.LoadIntoMemoryAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var engine = new DatabaseEngine(fs: memory);
        return (engine, virtualRoot, () => DatabaseLoader.SaveToDiskAsync(memory, databasePath, virtualRoot));
    }

    public async Task<EngineResult> ExecuteAsync(string commandText, string databasePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);

        object cmd;
        try
        {
            cmd = ParseSql(commandText);
        }
        catch (ParseException)
        {
            throw;
        }

        if (cmd is CreateDatabaseCommand createDb)
        {
            var dbRoot = _fs.Combine(resolvedPath, createDb.DatabaseName);
            _dbCreator.CreateDatabase(dbRoot, createDb.NumberFormat, createDb.TextEncoding, createDb.DefaultMaxShardSize, createDb.StorageBackend);
            return new EngineResult(0);
        }

        if (cmd is CreateTableCommand createTable)
        {
            await using (await _lockManager.AcquireWriteLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false))
                return await ExecuteCreateTableAsync(resolvedPath, createTable, cancellationToken).ConfigureAwait(false);
        }

        if (cmd is CreateIndexCommand createIndex)
        {
            await using (await _lockManager.AcquireWriteLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false))
                return await ExecuteCreateIndexAsync(resolvedPath, createIndex, cancellationToken).ConfigureAwait(false);
        }

        if (cmd is InsertCommand insertCmd)
        {
            await using var _schemaIns = await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            var insTable = await _schemaStore.ReadSchemaAsync(resolvedPath, insertCmd.TableName, cancellationToken).ConfigureAwait(false)
                ?? throw new SchemaException($"Table '{insertCmd.TableName}' not found");
            var readIns = GetFkParentTableNames(insTable);
            await using var _tblIns = await _lockManager.AcquireFkOrderedLocksAsync(resolvedPath, readIns, new[] { insertCmd.TableName }, cancellationToken).ConfigureAwait(false);
            return await ExecuteInsertAsync(resolvedPath, insertCmd, cancellationToken).ConfigureAwait(false);
        }

        if (cmd is UpdateCommand updateCmd)
        {
            await using var _schemaUpd = await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            var updTable = await _schemaStore.ReadSchemaAsync(resolvedPath, updateCmd.TableName, cancellationToken).ConfigureAwait(false)
                ?? throw new SchemaException($"Table '{updateCmd.TableName}' not found");
            var readUpd = GetFkParentTableNames(updTable);
            await using var _tblUpd = await _lockManager.AcquireFkOrderedLocksAsync(resolvedPath, readUpd, new[] { updateCmd.TableName }, cancellationToken).ConfigureAwait(false);
            return await ExecuteUpdateAsync(resolvedPath, updateCmd, cancellationToken).ConfigureAwait(false);
        }

        if (cmd is DeleteCommand deleteCmd)
        {
            await using var _schemaDel = await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            var delTable = await _schemaStore.ReadSchemaAsync(resolvedPath, deleteCmd.TableName, cancellationToken).ConfigureAwait(false)
                ?? throw new SchemaException($"Table '{deleteCmd.TableName}' not found");
            var readDel = GetFkParentTableNames(delTable);
            await using var _tblDel = await _lockManager.AcquireFkOrderedLocksAsync(resolvedPath, readDel, new[] { deleteCmd.TableName }, cancellationToken).ConfigureAwait(false);
            return await ExecuteDeleteAsync(resolvedPath, deleteCmd, cancellationToken).ConfigureAwait(false);
        }

        if (cmd is SelectCommand select)
        {
            if (select.WithNoLock)
                return await ExecuteSelectAsync(resolvedPath, select, null, cancellationToken).ConfigureAwait(false);

            await using var _schemaSel = await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            var selTables = CollectSelectInvolvedTables(select);
            await using var _tblSel = await _lockManager.AcquireTableReadLocksAsync(resolvedPath, selTables, cancellationToken).ConfigureAwait(false);
            long? snap = null;
            if (await MvccManifest.ReadMvccEnabledAsync(_fs, resolvedPath, cancellationToken).ConfigureAwait(false))
                snap = await _mvccXidStore.GetCommittedXidAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            return await ExecuteSelectAsync(resolvedPath, select, snap, cancellationToken).ConfigureAwait(false);
        }

        throw new ParseException($"Unsupported command type: {cmd.GetType().Name}");
    }

    private static IReadOnlyList<string> GetFkParentTableNames(TableDefinition table) =>
        table.ForeignKeys.Select(fk => fk.ReferencedTable).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<int> RebalanceTableAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);
        await EnsureDatabaseExistsAsync(resolvedPath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(resolvedPath, tableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{tableName}' not found");

        await using (await _lockManager.AcquireWriteLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false))
        {
            var warnings = new List<string>();
            var (total, active, deleted) = await _tableDataStore.StreamTransformRowsAsync(
                resolvedPath,
                tableName,
                r => r,
                cancellationToken,
                warnings).ConfigureAwait(false);

            await _metadataStore.UpdateMetadataAsync(resolvedPath, tableName, total, active, deleted, cancellationToken).ConfigureAwait(false);
            return active;
        }
    }

    public async Task<EngineResult> ExecuteQueryAsync(string queryText, string databasePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);

        object cmd;
        try
        {
            cmd = ParseSql(queryText);
        }
        catch (ParseException)
        {
            throw;
        }

        if (cmd is SelectCommand select)
        {
            if (select.WithNoLock)
                return await ExecuteSelectAsync(resolvedPath, select, null, cancellationToken).ConfigureAwait(false);

            await using var _schQ = await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            var qTables = CollectSelectInvolvedTables(select);
            await using var _tblQ = await _lockManager.AcquireTableReadLocksAsync(resolvedPath, qTables, cancellationToken).ConfigureAwait(false);
            long? snap = null;
            if (await MvccManifest.ReadMvccEnabledAsync(_fs, resolvedPath, cancellationToken).ConfigureAwait(false))
                snap = await _mvccXidStore.GetCommittedXidAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
            return await ExecuteSelectAsync(resolvedPath, select, snap, cancellationToken).ConfigureAwait(false);
        }

        throw new ParseException("Expected SELECT query");
    }

    public Task OpenAsync(string databasePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);
        if (!_fs.DirectoryExists(resolvedPath))
            throw new StorageException($"Database not found: {resolvedPath}");
        return Task.CompletedTask;
    }

    public async Task<ExecuteScriptResult> ExecuteScriptAsync(string scriptText, string databasePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);
        var batches = ScriptSplitter.SplitIntoBatches(scriptText);
        var totalExecuted = 0;
        var allWarnings = new List<string>();
        var queryResults = new List<QueryResult>();
        var currentDbPath = resolvedPath;

        foreach (var batch in batches)
        {
            foreach (var stmt in batch)
            {
                var trimmed = stmt.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                object cmd;
                try
                {
                    cmd = ParseSql(trimmed);
                }
                catch (ParseException)
                {
                    throw;
                }

                if (cmd is CreateDatabaseCommand cdb)
                {
                    await ExecuteAsync(trimmed, currentDbPath, cancellationToken).ConfigureAwait(false);
                    currentDbPath = _fs.Combine(currentDbPath, cdb.DatabaseName);
                    totalExecuted++;
                    continue;
                }

                var result = await ExecuteAsync(trimmed, currentDbPath, cancellationToken).ConfigureAwait(false);
                if (result.Warnings != null)
                    allWarnings.AddRange(result.Warnings);
                if (result.QueryResult != null)
                    queryResults.Add(result.QueryResult);
                totalExecuted++;
            }
        }

        return new ExecuteScriptResult(totalExecuted, allWarnings, queryResults);
    }

    public async Task<BuildSampleWikiResult> BuildSampleWikiAsync(string databasePath, BuildSampleWikiOptions? options = null, CancellationToken cancellationToken = default)
    {
        var opts = options ?? new BuildSampleWikiOptions();
        var resolvedPath = _fs.GetFullPath(databasePath);
        var wikiDbPath = _fs.Combine(resolvedPath, "WikiDb");
        var steps = new List<string>();
        var warnings = new List<string>();

        void AddStep(string msg)
        {
            if (opts.Verbose)
                steps.Add(msg);
        }

        if (opts.DeleteIfExists && _fs.DirectoryExists(wikiDbPath))
        {
            AddStep("Deleting existing WikiDb...");
            _fs.DeleteDirectory(wikiDbPath);
        }

        AddStep("Creating database WikiDb...");
        var createDbSql = opts.StorageBackend is "binary"
            ? "CREATE DATABASE WikiDb WITH (storageBackend=binary)"
            : "CREATE DATABASE WikiDb";
        await ExecuteAsync(createDbSql, resolvedPath, cancellationToken).ConfigureAwait(false);

        var createScript = LoadEmbeddedScript("SqlTxt.Engine.EmbeddedScripts.CreateWiki.sql");
        AddStep("Running schema script (CREATE TABLE)...");
        var scriptResult = await ExecuteScriptAsync(createScript, wikiDbPath, cancellationToken).ConfigureAwait(false);
        if (scriptResult.Warnings.Count > 0)
            warnings.AddRange(scriptResult.Warnings);

        var seedScript = LoadEmbeddedScript("SqlTxt.Engine.EmbeddedScripts.SeedWiki.sql");
        AddStep("Running seed script (INSERT)...");
        var seedResult = await ExecuteScriptAsync(seedScript, wikiDbPath, cancellationToken).ConfigureAwait(false);
        if (seedResult.Warnings.Count > 0)
            warnings.AddRange(seedResult.Warnings);

        var total = 1 + scriptResult.StatementsExecuted + seedResult.StatementsExecuted;
        AddStep($"Done. {total} statements executed.");

        return new BuildSampleWikiResult(total, steps, warnings);
    }

    private static string LoadEmbeddedScript(string resourceName)
    {
        var asm = typeof(DatabaseEngine).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private async Task<EngineResult> ExecuteCreateTableAsync(string databasePath, CreateTableCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var dbDefault = await _dbCreator.GetDefaultMaxShardSizeAsync(databasePath, cancellationToken).ConfigureAwait(false);
        var effectiveMaxShardSize = cmd.Table.MaxShardSize ?? dbDefault;
        var table = cmd.Table with { MaxShardSize = effectiveMaxShardSize };

        foreach (var fk in table.ForeignKeys)
        {
            var refTable = await _schemaStore.ReadSchemaAsync(databasePath, fk.ReferencedTable, cancellationToken).ConfigureAwait(false);
            if (refTable == null)
                throw new SchemaException($"Foreign key references non-existent table '{fk.ReferencedTable}'.");
            if (!refTable.Columns.Any(c => c.Name.Equals(fk.ReferencedColumn, StringComparison.OrdinalIgnoreCase)))
                throw new SchemaException($"Foreign key references non-existent column '{fk.ReferencedColumn}' in table '{fk.ReferencedTable}'.");
        }

        _dbCreator.CreateTableFolder(databasePath, table.TableName);
        await _schemaStore.WriteSchemaAsync(databasePath, table, cancellationToken).ConfigureAwait(false);
        await _metadataStore.UpdateMetadataAsync(databasePath, table.TableName, 0, 0, 0, cancellationToken).ConfigureAwait(false);

        if (table.PrimaryKey.Count > 0)
            await _indexStore.CreateIndexAsync(databasePath, table.TableName, "_PK", cancellationToken).ConfigureAwait(false);

        foreach (var fk in table.ForeignKeys)
        {
            var fkIndexName = $"_FK_{fk.ReferencedTable}";
            await _indexStore.CreateIndexAsync(databasePath, table.TableName, fkIndexName, cancellationToken).ConfigureAwait(false);
        }

        if (table.UniqueColumns.Count > 0)
        {
            var uqIndexName = "_UQ_" + string.Join("_", table.UniqueColumns);
            await _indexStore.CreateIndexAsync(databasePath, table.TableName, uqIndexName, cancellationToken).ConfigureAwait(false);
        }

        return new EngineResult(0);
    }

    private async Task<EngineResult> ExecuteCreateIndexAsync(string databasePath, CreateIndexCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");

        if (table.PrimaryKey.Count == 0 && table.ForeignKeys.Count == 0 && table.UniqueColumns.Count == 0)
            throw new SchemaException($"CREATE INDEX requires table '{cmd.TableName}' to have a primary key, foreign key, or unique constraint (for _RowId).");

        foreach (var col in cmd.ColumnNames)
        {
            if (!table.Columns.Any(c => c.Name.Equals(col, StringComparison.OrdinalIgnoreCase)))
                throw new SchemaException($"Column '{col}' not found in table '{cmd.TableName}'.");
        }

        var existingSameCols = table.Indexes
            .Where(i => i.ColumnNames.SequenceEqual(cmd.ColumnNames, StringComparer.OrdinalIgnoreCase))
            .ToList();
        var suffix = existingSameCols.Count > 0 ? existingSameCols.Count.ToString() : "0";
        var indexFileName = "_INX_" + string.Join("_", cmd.ColumnNames) + "_" + suffix;

        await _indexStore.CreateIndexAsync(databasePath, cmd.TableName, indexFileName, cancellationToken).ConfigureAwait(false);

        var allRows = await _tableDataStore.ReadAllRowsWithStatusAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);
        var rowsWithRowId = allRows.Where(r => r.IsActive).Select(r => r.Row).ToList();

        await _indexStore.RebuildIndexAsync(
            databasePath,
            cmd.TableName,
            table,
            indexFileName,
            cmd.ColumnNames,
            ToAsyncEnumerable(rowsWithRowId),
            cancellationToken).ConfigureAwait(false);

        var newIndex = new IndexDefinition(cmd.IndexName, cmd.TableName, cmd.ColumnNames.ToList(), cmd.IsUnique);
        var updatedTable = table with { IndexDefinitions = (table.IndexDefinitions ?? Array.Empty<IndexDefinition>()).Concat(new[] { newIndex }).ToList() };
        await _schemaStore.WriteSchemaAsync(databasePath, updatedTable, cancellationToken).ConfigureAwait(false);

        return new EngineResult(0);
    }

    private static async IAsyncEnumerable<RowData> ToAsyncEnumerable(IEnumerable<RowData> rows)
    {
        foreach (var row in rows)
            yield return row;
    }

    private async Task<EngineResult> ExecuteInsertAsync(string databasePath, InsertCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");

        var usesRowId = table.PrimaryKey.Count > 0 || table.ForeignKeys.Count > 0 || table.UniqueColumns.Count > 0;
        long rowIdStart = 0;
        var rowIdCount = 0;
        if (usesRowId && cmd.ValueRows.Count > 1)
        {
            var (start, count) = await _rowIdStore.GetNextRangeAndIncrementAsync(databasePath, cmd.TableName, cmd.ValueRows.Count, cancellationToken).ConfigureAwait(false);
            rowIdStart = start;
            rowIdCount = count;
        }

        var allWarnings = new List<string>();
        var materialized = new List<RowData>();
        var rowIndex = 0;

        foreach (var values in cmd.ValueRows)
        {
            var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < cmd.ColumnNames.Count; i++)
            {
                var colName = cmd.ColumnNames[i];
                var value = i < values.Count ? values[i] : string.Empty;
                var col = table.Columns.FirstOrDefault(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase))
                    ?? throw new SchemaException($"Column '{colName}' not found in table '{cmd.TableName}'");

                rowDict[colName] = value;
            }

            if (usesRowId)
            {
                long rowId;
                if (rowIdCount > 0)
                    rowId = rowIdStart + rowIndex;
                else
                    rowId = await _rowIdStore.GetNextAndIncrementAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);
                rowDict[TableDefinition.RowIdColumnName] = rowId.ToString();
                rowIndex++;
            }

            materialized.Add(new RowData(rowDict));
        }

        await ValidateInsertBatchAsync(databasePath, table, cmd.TableName, materialized, cancellationToken).ConfigureAwait(false);

        var mvccOn = await MvccManifest.ReadMvccEnabledAsync(_fs, databasePath, cancellationToken).ConfigureAwait(false);
        MvccRowVersions? mvccVers = null;
        var xid = 0L;
        if (mvccOn)
        {
            xid = await _mvccXidStore.AllocateXidAsync(databasePath, cancellationToken).ConfigureAwait(false);
            mvccVers = new MvccRowVersions(xid, 0);
        }

        var appendResults = await _tableDataStore.AppendRowsAsync(databasePath, cmd.TableName, materialized, cancellationToken, allWarnings, mvccVers).ConfigureAwait(false);

        if (mvccOn && xid > 0)
            await _mvccXidStore.CommitXidAsync(databasePath, xid, cancellationToken).ConfigureAwait(false);

        var indexBatches = new Dictionary<string, List<IndexEntry>>(StringComparer.Ordinal);

        void QueueIndex(string file, IndexEntry e)
        {
            if (!indexBatches.TryGetValue(file, out var list))
            {
                list = [];
                indexBatches[file] = list;
            }
            list.Add(e);
        }

        for (var i = 0; i < materialized.Count; i++)
        {
            var row = materialized[i];
            var (shardIndex, rowId) = appendResults[i];

            if (table.PrimaryKey.Count > 0)
            {
                var pkKey = IndexStore.FormatCompositeKey(table.PrimaryKey.Select(c => row.GetValue(c) ?? "").ToList());
                QueueIndex("_PK", new IndexEntry(pkKey, rowId, shardIndex));
            }

            foreach (var fk in table.ForeignKeys)
            {
                var fkValue = row.GetValue(fk.ColumnName) ?? "";
                QueueIndex($"_FK_{fk.ReferencedTable}", new IndexEntry(fkValue, rowId, shardIndex));
            }

            if (table.UniqueColumns.Count > 0)
            {
                var uqKey = IndexStore.FormatCompositeKey(table.UniqueColumns.Select(c => row.GetValue(c) ?? "").ToList());
                QueueIndex("_UQ_" + string.Join("_", table.UniqueColumns), new IndexEntry(uqKey, rowId, shardIndex));
            }

            foreach (var idx in table.Indexes)
            {
                var idxFileName = GetIndexFileNameForDefinition(table, idx);
                var key = IndexStore.FormatCompositeKeyFromRow(row, idx.ColumnNames);
                QueueIndex(idxFileName, new IndexEntry(key, rowId, shardIndex));
            }
        }

        foreach (var kv in indexBatches)
            await _indexStore.AddIndexEntriesAsync(databasePath, cmd.TableName, kv.Key, kv.Value, cancellationToken).ConfigureAwait(false);

        var insertedCount = materialized.Count;
        if (insertedCount > 0)
        {
            var (total, active, deleted) = await _metadataStore.ReadMetadataAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);
            await _metadataStore.UpdateMetadataAsync(databasePath, cmd.TableName, total + insertedCount, active + insertedCount, deleted, cancellationToken).ConfigureAwait(false);
        }

        return new EngineResult(insertedCount, Warnings: allWarnings.Count > 0 ? allWarnings : null);
    }

    private async Task ValidateInsertBatchAsync(string databasePath, TableDefinition table, string tableName, IReadOnlyList<RowData> rows, CancellationToken cancellationToken)
    {
        if (table.PrimaryKey.Count > 0)
        {
            var existing = await _indexStore.ReadAllKeyPrefixesAsync(databasePath, tableName, "_PK", cancellationToken).ConfigureAwait(false);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var pkKey = IndexStore.FormatCompositeKey(table.PrimaryKey.Select(c => row.GetValue(c) ?? "").ToList());
                if (!seen.Add(pkKey))
                    throw new ConstraintViolationException($"Duplicate primary key value in table '{tableName}'.");
                if (existing.Contains(pkKey))
                    throw new ConstraintViolationException($"Duplicate primary key value in table '{tableName}'.");
            }
        }

        foreach (var fk in table.ForeignKeys)
        {
            var parentKeys = await _indexStore.ReadAllKeyPrefixesAsync(databasePath, fk.ReferencedTable, "_PK", cancellationToken).ConfigureAwait(false);
            foreach (var row in rows)
            {
                var fkValue = row.GetValue(fk.ColumnName) ?? "";
                if (!parentKeys.Contains(fkValue))
                    throw new ConstraintViolationException($"Foreign key violation: '{fk.ReferencedTable}.{fk.ReferencedColumn}' has no row with value '{fkValue}'.");
            }
        }

        if (table.UniqueColumns.Count > 0)
        {
            var uqName = "_UQ_" + string.Join("_", table.UniqueColumns);
            var existingUq = await _indexStore.ReadAllKeyPrefixesAsync(databasePath, tableName, uqName, cancellationToken).ConfigureAwait(false);
            var seenUq = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var uqKey = IndexStore.FormatCompositeKey(table.UniqueColumns.Select(c => row.GetValue(c) ?? "").ToList());
                if (!seenUq.Add(uqKey))
                    throw new ConstraintViolationException($"Duplicate unique value in table '{tableName}'.");
                if (existingUq.Contains(uqKey))
                    throw new ConstraintViolationException($"Duplicate unique value in table '{tableName}'.");
            }
        }
    }

    private async Task<EngineResult> ExecuteSelectAsync(string databasePath, SelectCommand cmd, long? mvccSnapshotCommitted, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        if (cmd.Extensions != null)
            return await ExecuteSelectPhase4Async(databasePath, cmd, mvccSnapshotCommitted, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");

        var columnNames = cmd.ColumnNames ?? table.Columns.Select(c => c.Name).ToList();
        var rows = new List<RowData>();

        HashSet<long>? indexRowIds = null;
        if (cmd.WhereColumn != null && cmd.WhereValue != null)
        {
            var (indexName, keyValue) = ResolveIndexForWhere(table, cmd.WhereColumn, cmd.WhereValue);
            if (indexName != null)
            {
                var ids = await _indexStore.LookupByValueAsync(databasePath, cmd.TableName, indexName, keyValue, cancellationToken).ConfigureAwait(false);
                indexRowIds = ids.Count > 0 ? new HashSet<long>(ids) : new HashSet<long>();
            }
        }

        if (indexRowIds != null && indexRowIds.Count == 0)
        {
            return new EngineResult(0, new QueryResult(columnNames, rows));
        }

        var rowSource = indexRowIds != null && indexRowIds.Count > 0
            ? _tableDataStore.ReadRowsByRowIdsAsync(databasePath, cmd.TableName, indexRowIds, cancellationToken, mvccSnapshotCommitted)
            : _tableDataStore.ReadRowsAsync(databasePath, cmd.TableName, cancellationToken, mvccSnapshotCommitted);

        await foreach (var row in rowSource.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (cmd.WhereColumn != null && cmd.WhereValue != null && indexRowIds == null)
            {
                var rowVal = row.GetValue(cmd.WhereColumn);
                if (rowVal == null || !rowVal.Equals(cmd.WhereValue, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var projected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columnNames)
            {
                if (col.Equals(TableDefinition.RowIdColumnName, StringComparison.OrdinalIgnoreCase)
                    || col.Equals(TableDefinition.MvccXminKey, StringComparison.OrdinalIgnoreCase)
                    || col.Equals(TableDefinition.MvccXmaxKey, StringComparison.OrdinalIgnoreCase)
                    || col.Equals(TableDefinition.VacuumOmitKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                var val = row.GetValue(col);
                if (val != null)
                    projected[col] = val;
            }
            rows.Add(new RowData(projected));
        }

        return new EngineResult(0, new QueryResult(columnNames, rows));
    }

    private static string GetIndexFileNameForDefinition(TableDefinition table, IndexDefinition idx)
    {
        var sameCols = table.Indexes.Where(i => i.ColumnNames.SequenceEqual(idx.ColumnNames, StringComparer.OrdinalIgnoreCase)).ToList();
        var pos = sameCols.IndexOf(idx);
        return "_INX_" + string.Join("_", idx.ColumnNames) + "_" + (pos >= 0 ? pos : 0);
    }

    private static (string? IndexName, string KeyValue) ResolveIndexForWhere(TableDefinition table, string whereColumn, string whereValue)
    {
        if (table.PrimaryKey.Count == 1 && table.PrimaryKey[0].Equals(whereColumn, StringComparison.OrdinalIgnoreCase))
            return ("_PK", whereValue);
        if (table.UniqueColumns.Count == 1 && table.UniqueColumns[0].Equals(whereColumn, StringComparison.OrdinalIgnoreCase))
            return ("_UQ_" + string.Join("_", table.UniqueColumns), whereValue);
        var idx = table.Indexes.FirstOrDefault(i => i.ColumnNames.Count == 1 && i.ColumnNames[0].Equals(whereColumn, StringComparison.OrdinalIgnoreCase));
        if (idx != null)
        {
            var suffix = table.Indexes.TakeWhile(x => x != idx).Count(x => x.ColumnNames.SequenceEqual(idx.ColumnNames, StringComparer.OrdinalIgnoreCase));
            return ($"_INX_{string.Join("_", idx.ColumnNames)}_{suffix}", whereValue);
        }
        return (null, "");
    }

    private async Task<EngineResult> ExecuteUpdateAsync(string databasePath, UpdateCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");

        var warnings = new List<string>();
        var updated = 0;
        var pkUpdates = new List<(string OldKey, string NewKey, long RowId)>();

        var setTouchesPk = table.PrimaryKey.Count > 0 && cmd.SetClauses.Any(sc =>
            table.PrimaryKey.Any(pk => string.Equals(pk, sc.Item1, StringComparison.OrdinalIgnoreCase)));
        var setTouchesFk = table.ForeignKeys.Count > 0 && cmd.SetClauses.Any(sc =>
            table.ForeignKeys.Any(fk => string.Equals(fk.ColumnName, sc.Item1, StringComparison.OrdinalIgnoreCase)));
        var setTouchesUq = table.UniqueColumns.Count > 0 && cmd.SetClauses.Any(sc =>
            table.UniqueColumns.Any(uq => string.Equals(uq, sc.Item1, StringComparison.OrdinalIgnoreCase)));
        var setTouchesIndex = table.Indexes.Count > 0 && cmd.SetClauses.Any(sc =>
            table.Indexes.Any(idx => idx.ColumnNames.Any(c => string.Equals(c, sc.Item1, StringComparison.OrdinalIgnoreCase))));

        var fkUpdates = new List<(string OldValue, string NewValue, long RowId, string FkIndexName)>();
        var uqUpdates = new List<(string OldKey, string NewKey, long RowId)>();
        var indexUpdates = new List<(string OldKey, string NewKey, long RowId, string IndexFileName)>();

        var mvccOn = await MvccManifest.ReadMvccEnabledAsync(_fs, databasePath, cancellationToken).ConfigureAwait(false);
        var mvccXid = 0L;
        if (mvccOn)
            mvccXid = await _mvccXidStore.AllocateXidAsync(databasePath, cancellationToken).ConfigureAwait(false);

        (bool IsActive, RowData Row) Transform((bool IsActive, RowData Row) r)
        {
            if (!r.IsActive)
                return r;

            if (cmd.WhereColumn != null && cmd.WhereValue != null)
            {
                var rowVal = r.Row.GetValue(cmd.WhereColumn);
                if (rowVal == null || !rowVal.Equals(cmd.WhereValue, StringComparison.OrdinalIgnoreCase))
                    return r;
            }

            var dict = new Dictionary<string, string>(r.Row.Values, StringComparer.OrdinalIgnoreCase);
            foreach (var (col, value) in cmd.SetClauses)
            {
                var column = table.Columns.FirstOrDefault(c => c.Name.Equals(col, StringComparison.OrdinalIgnoreCase))
                    ?? throw new SchemaException($"Column '{col}' not found");
                dict[col] = value;
            }
            if (mvccOn && mvccXid > 0)
            {
                dict[TableDefinition.MvccXminKey] = mvccXid.ToString();
                dict[TableDefinition.MvccXmaxKey] = "0";
            }
            var newRow = new RowData(dict);

            if (setTouchesPk)
            {
                var oldKey = IndexStore.FormatCompositeKeyFromRow(r.Row, table.PrimaryKey);
                var newKey = IndexStore.FormatCompositeKeyFromRow(newRow, table.PrimaryKey);
                if (oldKey != newKey)
                {
                    var rowIdStr = r.Row.GetValue(TableDefinition.RowIdColumnName);
                    if (long.TryParse(rowIdStr, out var rid))
                        pkUpdates.Add((oldKey, newKey, rid));
                }
            }

            if (setTouchesFk)
            {
                foreach (var fk in table.ForeignKeys)
                {
                    var setCol = cmd.SetClauses.FirstOrDefault(sc => string.Equals(sc.Item1, fk.ColumnName, StringComparison.OrdinalIgnoreCase));
                    if (setCol.Item1 == null) continue;
                    var oldVal = r.Row.GetValue(fk.ColumnName) ?? "";
                    var newVal = setCol.Item2;
                    if (oldVal != newVal)
                    {
                        var rowIdStr = r.Row.GetValue(TableDefinition.RowIdColumnName);
                        if (long.TryParse(rowIdStr, out var rid))
                            fkUpdates.Add((oldVal, newVal, rid, $"_FK_{fk.ReferencedTable}"));
                    }
                }
            }

            if (setTouchesUq)
            {
                var oldUqKey = IndexStore.FormatCompositeKeyFromRow(r.Row, table.UniqueColumns);
                var newUqKey = IndexStore.FormatCompositeKeyFromRow(newRow, table.UniqueColumns);
                if (oldUqKey != newUqKey)
                {
                    var rowIdStr = r.Row.GetValue(TableDefinition.RowIdColumnName);
                    if (long.TryParse(rowIdStr, out var rid))
                        uqUpdates.Add((oldUqKey, newUqKey, rid));
                }
            }

            if (setTouchesIndex)
            {
                foreach (var idx in table.Indexes)
                {
                    var touches = idx.ColumnNames.Any(c => cmd.SetClauses.Any(sc => string.Equals(sc.Item1, c, StringComparison.OrdinalIgnoreCase)));
                    if (!touches) continue;
                    var oldKey = IndexStore.FormatCompositeKeyFromRow(r.Row, idx.ColumnNames);
                    var newKey = IndexStore.FormatCompositeKeyFromRow(newRow, idx.ColumnNames);
                    if (oldKey != newKey)
                    {
                        var rowIdStr = r.Row.GetValue(TableDefinition.RowIdColumnName);
                        if (long.TryParse(rowIdStr, out var rid))
                            indexUpdates.Add((oldKey, newKey, rid, GetIndexFileNameForDefinition(table, idx)));
                    }
                }
            }

            updated++;
            return (true, newRow);
        }

        var (total, active, delCount) = await _tableDataStore.StreamTransformRowsAsync(databasePath, cmd.TableName, Transform, cancellationToken, warnings).ConfigureAwait(false);

        if (mvccOn && mvccXid > 0)
            await _mvccXidStore.CommitXidAsync(databasePath, mvccXid, cancellationToken).ConfigureAwait(false);

        if (pkUpdates.Count > 0)
        {
            var updatingRowIds = new HashSet<long>(pkUpdates.Select(p => p.RowId));
            var newKeysSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, newKey, _) in pkUpdates)
            {
                if (!newKeysSeen.Add(newKey))
                    throw new ConstraintViolationException($"Duplicate primary key value in table '{cmd.TableName}'.");
                var existingIds = await _indexStore.LookupByValueAsync(databasePath, cmd.TableName, "_PK", newKey, cancellationToken).ConfigureAwait(false);
                var violators = existingIds.Where(id => !updatingRowIds.Contains(id)).ToList();
                if (violators.Count > 0)
                    throw new ConstraintViolationException($"Duplicate primary key value in table '{cmd.TableName}'.");
            }

            foreach (var (oldKey, _, _) in pkUpdates)
                await _indexStore.RemoveIndexEntryAsync(databasePath, cmd.TableName, "_PK", oldKey, cancellationToken).ConfigureAwait(false);

            foreach (var (_, newKey, rowId) in pkUpdates)
                await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, "_PK", newKey, rowId, shardId: 0, cancellationToken).ConfigureAwait(false);
        }

        foreach (var (oldVal, newVal, rowId, fkIndexName) in fkUpdates)
        {
            var refTable = fkIndexName["_FK_".Length..];
            var parentExists = await _indexStore.ContainsKeyAsync(databasePath, refTable, "_PK", newVal, cancellationToken).ConfigureAwait(false);
            if (!parentExists)
                throw new ConstraintViolationException($"Foreign key violation: '{refTable}' has no row with referenced value '{newVal}'.");
        }
        foreach (var (oldVal, newVal, rowId, fkIndexName) in fkUpdates)
        {
            await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, cmd.TableName, fkIndexName, oldVal, rowId, cancellationToken).ConfigureAwait(false);
            await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, fkIndexName, newVal, rowId, shardId: 0, cancellationToken).ConfigureAwait(false);
        }

        var uqIndexName = "_UQ_" + string.Join("_", table.UniqueColumns);
        if (uqUpdates.Count > 0)
        {
            var uqUpdatingRowIds = new HashSet<long>(uqUpdates.Select(u => u.RowId));
            var uqNewKeysSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (_, newKey, _) in uqUpdates)
            {
                if (!uqNewKeysSeen.Add(newKey))
                    throw new ConstraintViolationException($"Duplicate unique value in table '{cmd.TableName}'.");
                var existingIds = await _indexStore.LookupByValueAsync(databasePath, cmd.TableName, uqIndexName, newKey, cancellationToken).ConfigureAwait(false);
                if (existingIds.Any(id => !uqUpdatingRowIds.Contains(id)))
                    throw new ConstraintViolationException($"Duplicate unique value in table '{cmd.TableName}'.");
            }
            foreach (var (oldKey, newKey, rowId) in uqUpdates)
            {
                await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, cmd.TableName, uqIndexName, oldKey, rowId, cancellationToken).ConfigureAwait(false);
                await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, uqIndexName, newKey, rowId, shardId: 0, cancellationToken).ConfigureAwait(false);
            }
        }

        foreach (var (oldKey, newKey, rowId, idxFileName) in indexUpdates)
        {
            await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, cmd.TableName, idxFileName, oldKey, rowId, cancellationToken).ConfigureAwait(false);
            await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, idxFileName, newKey, rowId, shardId: 0, cancellationToken).ConfigureAwait(false);
        }

        await _metadataStore.UpdateMetadataAsync(databasePath, cmd.TableName, total, active, delCount, cancellationToken).ConfigureAwait(false);

        return new EngineResult(updated, Warnings: warnings);
    }

    private async Task<EngineResult> ExecuteDeleteAsync(string databasePath, DeleteCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);

        var deleted = 0;
        var pkRemovals = new List<string>();
        var fkRemovals = new List<(string TableName, string FkIndexName, string KeyValue, long RowId)>();
        var uqRemovals = new List<(string KeyValue, long RowId)>();
        var indexRemovals = new List<(string IndexFileName, string KeyValue, long RowId)>();

        if (table != null && table.PrimaryKey.Count > 0)
        {
            var allTables = await _schemaStore.GetTableNamesAsync(databasePath, cancellationToken).ConfigureAwait(false);
            var childTables = new List<(string TableName, string FkColumnName)>();
            foreach (var t in allTables)
            {
                var childSchema = await _schemaStore.ReadSchemaAsync(databasePath, t, cancellationToken).ConfigureAwait(false);
                if (childSchema == null) continue;
                foreach (var fk in childSchema.ForeignKeys)
                {
                    if (string.Equals(fk.ReferencedTable, cmd.TableName, StringComparison.OrdinalIgnoreCase))
                        childTables.Add((t, fk.ColumnName));
                }
            }

            var rowsToDelete = await _tableDataStore.ReadAllRowsWithStatusAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);
            foreach (var (isActive, row) in rowsToDelete)
            {
                if (!isActive) continue;
                if (cmd.WhereColumn != null && cmd.WhereValue != null)
                {
                    var rowVal = row.GetValue(cmd.WhereColumn);
                    if (rowVal == null || !rowVal.Equals(cmd.WhereValue, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                var pkKey = IndexStore.FormatCompositeKeyFromRow(row, table.PrimaryKey);
                foreach (var (childTable, _) in childTables)
                {
                    var childSchema = await _schemaStore.ReadSchemaAsync(databasePath, childTable, cancellationToken).ConfigureAwait(false);
                    var fkDef = childSchema!.ForeignKeys.First(f => string.Equals(f.ReferencedTable, cmd.TableName, StringComparison.OrdinalIgnoreCase));
                    var fkIndexName = $"_FK_{fkDef.ReferencedTable}";
                    var refs = await _indexStore.LookupByValueAsync(databasePath, childTable, fkIndexName, pkKey, cancellationToken).ConfigureAwait(false);
                    if (refs.Count > 0)
                        throw new ConstraintViolationException($"Cannot delete: table '{childTable}' has rows referencing this row.");
                }
            }
        }

        (bool IsActive, RowData Row) Transform((bool IsActive, RowData Row) r)
        {
            if (!r.IsActive)
                return r;

            if (cmd.WhereColumn != null && cmd.WhereValue != null)
            {
                var rowVal = r.Row.GetValue(cmd.WhereColumn);
                if (rowVal == null || !rowVal.Equals(cmd.WhereValue, StringComparison.OrdinalIgnoreCase))
                    return r;
            }

            if (table != null && table.PrimaryKey.Count > 0)
            {
                var pkKey = IndexStore.FormatCompositeKeyFromRow(r.Row, table.PrimaryKey);
                pkRemovals.Add(pkKey);
            }

            if (table != null)
            {
                var rowIdStr = r.Row.GetValue(TableDefinition.RowIdColumnName);
                if (long.TryParse(rowIdStr, out var rowId))
                {
                    foreach (var fk in table.ForeignKeys)
                    {
                        var fkValue = r.Row.GetValue(fk.ColumnName) ?? "";
                        fkRemovals.Add((cmd.TableName, $"_FK_{fk.ReferencedTable}", fkValue, rowId));
                    }
                    if (table.UniqueColumns.Count > 0)
                    {
                        var uqKey = IndexStore.FormatCompositeKeyFromRow(r.Row, table.UniqueColumns);
                        uqRemovals.Add((uqKey, rowId));
                    }
                    foreach (var idx in table.Indexes)
                    {
                        var idxFileName = GetIndexFileNameForDefinition(table, idx);
                        var key = IndexStore.FormatCompositeKeyFromRow(r.Row, idx.ColumnNames);
                        indexRemovals.Add((idxFileName, key, rowId));
                    }
                }
            }

            deleted++;
            return (false, r.Row); // Mark as deleted (D|)
        }

        var (total, active, delCount) = await _tableDataStore.StreamTransformRowsAsync(databasePath, cmd.TableName, Transform, cancellationToken).ConfigureAwait(false);

        foreach (var pkKey in pkRemovals)
            await _indexStore.RemoveIndexEntryAsync(databasePath, cmd.TableName, "_PK", pkKey, cancellationToken).ConfigureAwait(false);

        foreach (var (tbl, fkIdx, keyVal, rowId) in fkRemovals)
            await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, tbl, fkIdx, keyVal, rowId, cancellationToken).ConfigureAwait(false);

        if (table != null && table.UniqueColumns.Count > 0)
        {
            var uqIndexName = "_UQ_" + string.Join("_", table.UniqueColumns);
            foreach (var (keyVal, rowId) in uqRemovals)
                await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, cmd.TableName, uqIndexName, keyVal, rowId, cancellationToken).ConfigureAwait(false);
        }

        foreach (var (idxFileName, keyVal, rowId) in indexRemovals)
            await _indexStore.RemoveIndexEntryByValueAndRowIdAsync(databasePath, cmd.TableName, idxFileName, keyVal, rowId, cancellationToken).ConfigureAwait(false);

        await _metadataStore.UpdateMetadataAsync(databasePath, cmd.TableName, total, active, delCount, cancellationToken).ConfigureAwait(false);

        return new EngineResult(deleted);
    }

    /// <inheritdoc />
    public async Task<int> VacuumMvccRowsAsync(string databasePath, string tableName, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);
        await EnsureDatabaseExistsAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        if (!await MvccManifest.ReadMvccEnabledAsync(_fs, resolvedPath, cancellationToken).ConfigureAwait(false))
            return 0;

        var committed = await _mvccXidStore.GetCommittedXidAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        await using var _sch = await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false);
        await using var _tbl = await _lockManager.AcquireTableWriteLocksAsync(resolvedPath, new[] { tableName }, cancellationToken).ConfigureAwait(false);

        var removed = 0;
        (bool IsActive, RowData Row) Transform((bool IsActive, RowData Row) r)
        {
            if (!r.IsActive)
                return r;
            var (_, xmax) = RowMvccHelper.GetXminXmax(r.Row);
            if (xmax > 0 && xmax <= committed)
            {
                removed++;
                var d = new Dictionary<string, string>(r.Row.Values, StringComparer.OrdinalIgnoreCase)
                {
                    [TableDefinition.VacuumOmitKey] = "1"
                };
                return (r.IsActive, new RowData(d));
            }
            return r;
        }

        await _tableDataStore.StreamTransformRowsAsync(resolvedPath, tableName, Transform, cancellationToken).ConfigureAwait(false);
        return removed;
    }

    private async Task EnsureDatabaseExistsAsync(string databasePath, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        var dbDir = _fs.Combine(databasePath, "db");
        if (!_fs.DirectoryExists(dbDir))
            throw new StorageException($"Database not found: {databasePath}");
    }
}
