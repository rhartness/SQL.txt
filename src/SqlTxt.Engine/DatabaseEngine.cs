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
    private readonly ICommandParser _parser;
    private readonly IDatabaseLockManager _lockManager;
    private readonly Contracts.IFileSystemAccessor _fs;
    private readonly DatabaseCreator _dbCreator;
    private readonly SchemaStore _schemaStore;
    private readonly TableDataStore _tableDataStore;
    private readonly MetadataStore _metadataStore;

    public DatabaseEngine(
        ICommandParser? parser = null,
        IDatabaseLockManager? lockManager = null,
        Contracts.IFileSystemAccessor? fs = null)
    {
        _parser = parser ?? new SqlCommandParser();
        _lockManager = lockManager ?? new DatabaseLockManager();
        _fs = fs ?? new FileSystemAccessor();

        var serializer = new FixedWidthRowSerializer();
        var deserializer = new FixedWidthRowDeserializer();
        _schemaStore = new SchemaStore(_fs);
        _metadataStore = new MetadataStore(_fs);
        _tableDataStore = new TableDataStore(_fs, serializer, deserializer, _schemaStore);
        _dbCreator = new DatabaseCreator(_fs);
    }

    public async Task<EngineResult> ExecuteAsync(string commandText, string databasePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);

        object cmd;
        try
        {
            cmd = _parser.Parse(commandText);
        }
        catch (ParseException)
        {
            throw;
        }

        if (cmd is CreateDatabaseCommand createDb)
        {
            var dbRoot = _fs.Combine(resolvedPath, createDb.DatabaseName);
            _dbCreator.CreateDatabase(dbRoot, createDb.NumberFormat, createDb.TextEncoding);
            return new EngineResult(0);
        }

        await using (await _lockManager.AcquireWriteLockAsync(resolvedPath, cancellationToken).ConfigureAwait(false))
        {
            if (cmd is CreateTableCommand createTable)
                return await ExecuteCreateTableAsync(resolvedPath, createTable, cancellationToken).ConfigureAwait(false);
            if (cmd is InsertCommand insert)
                return await ExecuteInsertAsync(resolvedPath, insert, cancellationToken).ConfigureAwait(false);
            if (cmd is UpdateCommand update)
                return await ExecuteUpdateAsync(resolvedPath, update, cancellationToken).ConfigureAwait(false);
            if (cmd is DeleteCommand delete)
                return await ExecuteDeleteAsync(resolvedPath, delete, cancellationToken).ConfigureAwait(false);
        }

        if (cmd is SelectCommand select)
            return await ExecuteSelectAsync(resolvedPath, select, cancellationToken).ConfigureAwait(false);

        throw new ParseException($"Unsupported command type: {cmd.GetType().Name}");
    }

    public async Task<EngineResult> ExecuteQueryAsync(string queryText, string databasePath, CancellationToken cancellationToken = default)
    {
        var resolvedPath = _fs.GetFullPath(databasePath);

        object cmd;
        try
        {
            cmd = _parser.Parse(queryText);
        }
        catch (ParseException)
        {
            throw;
        }

        if (cmd is SelectCommand select)
            return await ExecuteSelectAsync(resolvedPath, select, cancellationToken).ConfigureAwait(false);

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

                var cmd = _parser.Parse(trimmed);

                if (cmd is CreateDatabaseCommand createDb)
                {
                    var parent = Path.GetDirectoryName(currentDbPath) ?? currentDbPath;
                    var dbRoot = _fs.Combine(parent, createDb.DatabaseName);
                    _dbCreator.CreateDatabase(dbRoot, createDb.NumberFormat, createDb.TextEncoding);
                    currentDbPath = dbRoot;
                    totalExecuted++;
                    continue;
                }

                if (cmd is SelectCommand select)
                {
                    var result = await ExecuteSelectAsync(currentDbPath, select, cancellationToken).ConfigureAwait(false);
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

        var table = cmd.Table;
        _dbCreator.CreateTableFolder(databasePath, table.TableName);
        await _schemaStore.WriteSchemaAsync(databasePath, table, cancellationToken).ConfigureAwait(false);
        await _metadataStore.UpdateMetadataAsync(databasePath, table.TableName, 0, 0, 0, cancellationToken).ConfigureAwait(false);

        return new EngineResult(0);
    }

    private async Task<EngineResult> ExecuteInsertAsync(string databasePath, InsertCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");

        var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < cmd.ColumnNames.Count; i++)
        {
            var colName = cmd.ColumnNames[i];
            var value = i < cmd.Values.Count ? cmd.Values[i] : string.Empty;
            var col = table.Columns.FirstOrDefault(c => c.Name.Equals(colName, StringComparison.OrdinalIgnoreCase))
                ?? throw new SchemaException($"Column '{colName}' not found in table '{cmd.TableName}'");

            rowDict[colName] = value;
        }

        var row = new RowData(rowDict);
        var warnings = new List<string>();
        await _tableDataStore.AppendRowAsync(databasePath, cmd.TableName, row, cancellationToken, warnings).ConfigureAwait(false);

        var (total, active, deleted) = await _metadataStore.ReadMetadataAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);
        await _metadataStore.UpdateMetadataAsync(databasePath, cmd.TableName, total + 1, active + 1, deleted, cancellationToken).ConfigureAwait(false);

        return new EngineResult(1, Warnings: warnings);
    }

    private async Task<EngineResult> ExecuteSelectAsync(string databasePath, SelectCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");

        var columnNames = cmd.ColumnNames ?? table.Columns.Select(c => c.Name).ToList();
        var rows = new List<RowData>();

        await foreach (var row in _tableDataStore.ReadRowsAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false))
        {
            if (cmd.WhereColumn != null && cmd.WhereValue != null)
            {
                var rowVal = row.GetValue(cmd.WhereColumn);
                if (rowVal == null || !rowVal.Equals(cmd.WhereValue, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var projected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var col in columnNames)
            {
                var val = row.GetValue(col);
                if (val != null)
                    projected[col] = val;
            }
            rows.Add(new RowData(projected));
        }

        return new EngineResult(0, new QueryResult(columnNames, rows));
    }

    private async Task<EngineResult> ExecuteUpdateAsync(string databasePath, UpdateCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        var table = await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false)
            ?? throw new SchemaException($"Table '{cmd.TableName}' not found");

        var warnings = new List<string>();
        var updated = 0;

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
            updated++;
            return (true, new RowData(dict));
        }

        var (total, active, delCount) = await _tableDataStore.StreamTransformRowsAsync(databasePath, cmd.TableName, Transform, cancellationToken, warnings).ConfigureAwait(false);
        await _metadataStore.UpdateMetadataAsync(databasePath, cmd.TableName, total, active, delCount, cancellationToken).ConfigureAwait(false);

        return new EngineResult(updated, Warnings: warnings);
    }

    private async Task<EngineResult> ExecuteDeleteAsync(string databasePath, DeleteCommand cmd, CancellationToken cancellationToken)
    {
        await EnsureDatabaseExistsAsync(databasePath, cancellationToken).ConfigureAwait(false);

        await _schemaStore.ReadSchemaAsync(databasePath, cmd.TableName, cancellationToken).ConfigureAwait(false);

        var deleted = 0;

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

            deleted++;
            return (false, r.Row); // Mark as deleted (D|)
        }

        var (total, active, delCount) = await _tableDataStore.StreamTransformRowsAsync(databasePath, cmd.TableName, Transform, cancellationToken).ConfigureAwait(false);
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
