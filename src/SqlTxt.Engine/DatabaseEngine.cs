using SqlTxt.Contracts;
using SqlTxt.Contracts.Commands;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Parser;
using SqlTxt.Storage;

namespace SqlTxt.Engine;

/// <summary>
/// Main database engine implementation.
/// </summary>
public sealed class DatabaseEngine : IDatabaseEngine
{
    private readonly ICommandParser? _parser;
    private readonly IDatabaseLockManager _lockManager;
    private readonly Contracts.IFileSystemAccessor _fs;
    private readonly DatabaseCreator _dbCreator;
    private readonly ISchemaStore _schemaStore;
    private readonly TableDataStore _tableDataStore;
    private readonly MetadataStore _metadataStore;
    private readonly IIndexStore _indexStore;
    private readonly IRowIdSequenceStore _rowIdStore;

    public DatabaseEngine(
        ICommandParser? parser = null,
        IDatabaseLockManager? lockManager = null,
        Contracts.IFileSystemAccessor? fs = null,
        IIndexStore? indexStore = null,
        IRowIdSequenceStore? rowIdStore = null)
    {
        _parser = parser;
        _lockManager = lockManager ?? new DatabaseLockManager();
        _fs = fs ?? new FileSystemAccessor();
        _indexStore = indexStore ?? new IndexStore(_fs);
        _rowIdStore = rowIdStore ?? new RowIdSequenceStore(_fs);

        var serializer = new FixedWidthRowSerializer();
        var deserializer = new FixedWidthRowDeserializer();
        var schemaStore = new SchemaStore(_fs);
        _schemaStore = new CachingSchemaStore(schemaStore);
        _metadataStore = new MetadataStore(_fs);
        _tableDataStore = new TableDataStore(_fs, serializer, deserializer, _schemaStore, _rowIdStore, stocStore: null, indexStore: _indexStore);
        _dbCreator = new DatabaseCreator(_fs);
    }

    public async Task<EngineResult> ExecuteAsync(string commandText, string databasePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);

        object cmd;
        try
        {
            var parser = _parser ?? new SqlCommandParser();
            cmd = parser.Parse(commandText);
        }
        catch (ParseException)
        {
            throw;
        }

        if (cmd is CreateDatabaseCommand createDb)
        {
            var dbRoot = _fs.Combine(resolvedPath, createDb.DatabaseName);
            _dbCreator.CreateDatabase(dbRoot, createDb.NumberFormat, createDb.TextEncoding, createDb.DefaultMaxShardSize);
            return new EngineResult(0);
        }

        await using (await _lockManager.AcquireWriteLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false))
        {
            if (cmd is CreateTableCommand createTable)
                return await ExecuteCreateTableAsync(resolvedPath, createTable, cancellationToken).ConfigureAwait(false);
            if (cmd is CreateIndexCommand createIndex)
                return await ExecuteCreateIndexAsync(resolvedPath, createIndex, cancellationToken).ConfigureAwait(false);
            if (cmd is InsertCommand insert)
                return await ExecuteInsertAsync(resolvedPath, insert, cancellationToken).ConfigureAwait(false);
            if (cmd is UpdateCommand update)
                return await ExecuteUpdateAsync(resolvedPath, update, cancellationToken).ConfigureAwait(false);
            if (cmd is DeleteCommand delete)
                return await ExecuteDeleteAsync(resolvedPath, delete, cancellationToken).ConfigureAwait(false);
        }

        if (cmd is SelectCommand select)
        {
            if (!select.WithNoLock)
            {
                await using (await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false))
                    return await ExecuteSelectAsync(resolvedPath, select, cancellationToken).ConfigureAwait(false);
            }
            return await ExecuteSelectAsync(resolvedPath, select, cancellationToken).ConfigureAwait(false);
        }

        throw new ParseException($"Unsupported command type: {cmd.GetType().Name}");
    }

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
            var parser = _parser ?? new SqlCommandParser();
            cmd = parser.Parse(queryText);
        }
        catch (ParseException)
        {
            throw;
        }

        if (cmd is SelectCommand select)
        {
            if (!select.WithNoLock)
            {
                await using (await _lockManager.AcquireReadLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false))
                    return await ExecuteSelectAsync(resolvedPath, select, cancellationToken).ConfigureAwait(false);
            }
            return await ExecuteSelectAsync(resolvedPath, select, cancellationToken).ConfigureAwait(false);
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

                var parser = _parser ?? new SqlCommandParser();
                var cmd = parser.Parse(trimmed);

                if (cmd is CreateDatabaseCommand createDb)
                {
                    var parent = Path.GetDirectoryName(currentDbPath) ?? currentDbPath;
                    var dbRoot = _fs.Combine(parent, createDb.DatabaseName);
                    _dbCreator.CreateDatabase(dbRoot, createDb.NumberFormat, createDb.TextEncoding, createDb.DefaultMaxShardSize);
                    currentDbPath = dbRoot;
                    totalExecuted++;
                    continue;
                }

                if (cmd is SelectCommand select)
                {
                    EngineResult result;
                    if (select.WithNoLock)
                        result = await ExecuteSelectAsync(currentDbPath, select, cancellationToken).ConfigureAwait(false);
                    else
                    {
                        await using (await _lockManager.AcquireReadLockAsync(currentDbPath, cancellationToken).ConfigureAwait(false))
                            result = await ExecuteSelectAsync(currentDbPath, select, cancellationToken).ConfigureAwait(false);
                    }
                    if (result.Warnings != null)
                        allWarnings.AddRange(result.Warnings);
                    if (result.QueryResult != null)
                        queryResults.Add(result.QueryResult);
                    totalExecuted++;
                    continue;
                }

                await using (await _lockManager.AcquireWriteLockAsync(currentDbPath, cancellationToken).ConfigureAwait(false))
                {
                    if (cmd is CreateTableCommand createTable)
                    {
                        await ExecuteCreateTableAsync(currentDbPath, createTable, cancellationToken).ConfigureAwait(false);
                        totalExecuted++;
                    }
                    else if (cmd is CreateIndexCommand createIndex)
                    {
                        await ExecuteCreateIndexAsync(currentDbPath, createIndex, cancellationToken).ConfigureAwait(false);
                        totalExecuted++;
                    }
                    else if (cmd is InsertCommand insert)
                    {
                        var r = await ExecuteInsertAsync(currentDbPath, insert, cancellationToken).ConfigureAwait(false);
                        if (r.Warnings != null)
                            allWarnings.AddRange(r.Warnings);
                        totalExecuted++;
                    }
                    else if (cmd is UpdateCommand update)
                    {
                        var r = await ExecuteUpdateAsync(currentDbPath, update, cancellationToken).ConfigureAwait(false);
                        if (r.Warnings != null)
                            allWarnings.AddRange(r.Warnings);
                        totalExecuted++;
                    }
                    else if (cmd is DeleteCommand delete)
                    {
                        await ExecuteDeleteAsync(currentDbPath, delete, cancellationToken).ConfigureAwait(false);
                        totalExecuted++;
                    }
                    else
                    {
                        throw new ParseException($"Unsupported command in script: {cmd.GetType().Name}");
                    }
                }
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
        await ExecuteAsync("CREATE DATABASE WikiDb", resolvedPath, cancellationToken).ConfigureAwait(false);

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

        var allWarnings = new List<string>();
        var insertedCount = 0;

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

            var row = new RowData(rowDict);

            if (table.PrimaryKey.Count > 0)
            {
                var pkValues = table.PrimaryKey.Select(c => rowDict.TryGetValue(c, out var v) ? v : "").ToList();
                var pkKey = IndexStore.FormatCompositeKey(pkValues);
                var exists = await _indexStore.ContainsKeyAsync(databasePath, cmd.TableName, "_PK", pkKey, cancellationToken).ConfigureAwait(false);
                if (exists)
                    throw new ConstraintViolationException($"Duplicate primary key value in table '{cmd.TableName}'.");
            }

            foreach (var fk in table.ForeignKeys)
            {
                var fkValue = rowDict.TryGetValue(fk.ColumnName, out var fkv) ? fkv : "";
                var parentExists = await _indexStore.ContainsKeyAsync(databasePath, fk.ReferencedTable, "_PK", fkValue, cancellationToken).ConfigureAwait(false);
                if (!parentExists)
                    throw new ConstraintViolationException($"Foreign key violation: '{fk.ReferencedTable}.{fk.ReferencedColumn}' has no row with value '{fkValue}'.");
            }

            if (table.UniqueColumns.Count > 0)
            {
                var uqKey = IndexStore.FormatCompositeKey(table.UniqueColumns.Select(c => rowDict.TryGetValue(c, out var v) ? v : "").ToList());
                var uqExists = await _indexStore.ContainsKeyAsync(databasePath, cmd.TableName, "_UQ_" + string.Join("_", table.UniqueColumns), uqKey, cancellationToken).ConfigureAwait(false);
                if (uqExists)
                    throw new ConstraintViolationException($"Duplicate unique value in table '{cmd.TableName}'.");
            }

            long rowId = 0;
            if (table.PrimaryKey.Count > 0 || table.ForeignKeys.Count > 0 || table.UniqueColumns.Count > 0)
            {
                rowId = await _rowIdStore.GetNextAndIncrementAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);
                rowDict[TableDefinition.RowIdColumnName] = rowId.ToString();
                row = new RowData(rowDict);
            }

            var warnings = new List<string>();
            var (shardIndex, appendedRowId) = await _tableDataStore.AppendRowAsync(databasePath, cmd.TableName, row, cancellationToken, warnings).ConfigureAwait(false);
            rowId = appendedRowId;
            if (warnings.Count > 0)
                allWarnings.AddRange(warnings);

            if (table.PrimaryKey.Count > 0)
            {
                var pkKey = IndexStore.FormatCompositeKey(table.PrimaryKey.Select(c => row.GetValue(c) ?? "").ToList());
                await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, "_PK", pkKey, rowId, shardIndex, cancellationToken).ConfigureAwait(false);
            }

            foreach (var fk in table.ForeignKeys)
            {
                var fkValue = row.GetValue(fk.ColumnName) ?? "";
                await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, $"_FK_{fk.ReferencedTable}", fkValue, rowId, shardIndex, cancellationToken).ConfigureAwait(false);
            }

            if (table.UniqueColumns.Count > 0)
            {
                var uqKey = IndexStore.FormatCompositeKey(table.UniqueColumns.Select(c => row.GetValue(c) ?? "").ToList());
                await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, "_UQ_" + string.Join("_", table.UniqueColumns), uqKey, rowId, shardIndex, cancellationToken).ConfigureAwait(false);
            }

            foreach (var idx in table.Indexes)
            {
                var idxFileName = GetIndexFileNameForDefinition(table, idx);
                var key = IndexStore.FormatCompositeKeyFromRow(row, idx.ColumnNames);
                await _indexStore.AddIndexEntryAsync(databasePath, cmd.TableName, idxFileName, key, rowId, shardIndex, cancellationToken).ConfigureAwait(false);
            }

            insertedCount++;
        }

        if (insertedCount > 0)
        {
            var (total, active, deleted) = await _metadataStore.ReadMetadataAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);
            await _metadataStore.UpdateMetadataAsync(databasePath, cmd.TableName, total + insertedCount, active + insertedCount, deleted, cancellationToken).ConfigureAwait(false);
        }

        return new EngineResult(insertedCount, Warnings: allWarnings.Count > 0 ? allWarnings : null);
    }

    private async Task<EngineResult> ExecuteSelectAsync(string databasePath, SelectCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

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

        await foreach (var row in _tableDataStore.ReadRowsAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false))
        {
            if (cmd.WhereColumn != null && cmd.WhereValue != null)
            {
                if (indexRowIds != null)
                {
                    var rowIdStr = row.GetValue(TableDefinition.RowIdColumnName);
                    if (string.IsNullOrEmpty(rowIdStr) || !long.TryParse(rowIdStr, out var rid) || !indexRowIds.Contains(rid))
                        continue;
                }
                else
                {
                    var rowVal = row.GetValue(cmd.WhereColumn);
                    if (rowVal == null || !rowVal.Equals(cmd.WhereValue, StringComparison.OrdinalIgnoreCase))
                        continue;
                }
            }

            var projected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columnNames)
            {
                if (col.Equals(TableDefinition.RowIdColumnName, StringComparison.OrdinalIgnoreCase))
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

    private async Task EnsureDatabaseExistsAsync(string databasePath, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        var dbDir = _fs.Combine(databasePath, "db");
        if (!_fs.DirectoryExists(dbDir))
            throw new StorageException($"Database not found: {databasePath}");
    }
}
