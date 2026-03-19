using SqlTxt.Contracts;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.Engine;
using SqlTxt.Storage;

var argsList = args.ToList();
if (argsList.Count == 0)
{
    PrintUsage();
    return 1;
}

var command = argsList[0].ToLowerInvariant();
argsList.RemoveAt(0);

try
{
    return command switch
    {
        "create-db" => await CreateDatabaseAsync(argsList),
        "build-sample-wiki" => await BuildSampleWikiAsync(argsList),
        "exec" => await ExecAsync(argsList),
        "query" => await QueryAsync(argsList),
        "script" => await ScriptAsync(argsList),
        "inspect" => await InspectAsync(argsList),
        "rebalance" => await RebalanceAsync(argsList),
        _ => UnknownCommand(command)
    };
}
catch (ParseException ex)
{
    Console.Error.WriteLine($"Parse error: {ex.Message}");
    if (ex.Line.HasValue)
        Console.Error.WriteLine($"  at line {ex.Line}, column {ex.Column}");
    return 2;
}
catch (SchemaException ex)
{
    Console.Error.WriteLine($"Schema error: {ex.Message}");
    return 3;
}
catch (ValidationException ex)
{
    Console.Error.WriteLine($"Validation error: {ex.Message}");
    return 4;
}
catch (StorageException ex)
{
    Console.Error.WriteLine($"Storage error: {ex.Message}");
    return 5;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 99;
}

static void PrintUsage()
{
    Console.WriteLine("""
        SQL.txt CLI - Lightweight text-file database

        Usage:
          sqltxt create-db <path> [--wasm] [--storage:text|binary]  Create database at path
          sqltxt build-sample-wiki [--db <path>] [--wasm] Build sample Wiki database
          sqltxt exec --db <path> [--wasm] "<statement>" Execute single statement
          sqltxt query --db <path> [--wasm] "<select>"   Execute SELECT and print results
          sqltxt script --db <path> [--wasm] <file>     Execute script file
          sqltxt inspect --db <path> [--wasm]           List tables and row counts
          sqltxt rebalance --db <path> --table <name>   Rebalance shards for a table

        Options:
          --wasm  Use WASM-compatible in-memory storage (persisted to .wasmdb file)
          --memory  Load filesystem DB into memory; operate in RAM (use with --persist to flush on exit)
          --persist  With --memory: save changes back to disk when done
          --storage:text   Human-readable files (default)
          --storage:binary Compact binary files for performance

        Examples:
          sqltxt create-db ./WikiDb
          sqltxt create-db ./WikiDb --wasm
          sqltxt exec --db ./WikiDb "CREATE TABLE User (Id CHAR(10), Name CHAR(50))"
          sqltxt query --db ./WikiDb.wasmdb --wasm "SELECT * FROM User"
        """);
}

static async Task<int> BuildSampleWikiAsync(List<string> args)
{
    var (dbPath, useWasm, _, _, _) = ExtractDbPathAndOptions(args);
    var path = dbPath ?? Path.GetFullPath(".");
    (IDatabaseEngine engine, string enginePath) = useWasm
        ? CreateEngineForBuildSampleWiki(path)
        : (new DatabaseEngine(), path);
    var result = await engine.BuildSampleWikiAsync(enginePath, new BuildSampleWikiOptions(Verbose: true, DeleteIfExists: true));
    foreach (var step in result.Steps)
        Console.WriteLine(step);
    foreach (var w in result.Warnings)
        Console.Error.WriteLine($"Warning: {w}");
    Console.WriteLine(useWasm ? $"Built sample Wiki database: {Path.Combine(Path.GetFullPath(path), "WikiDb.wasmdb")}" : $"Built sample Wiki database: {Path.Combine(path, "WikiDb")}");
    Console.WriteLine($"{result.StatementsExecuted} statements executed.");
    return 0;
}

static async Task<int> CreateDatabaseAsync(List<string> args)
{
    var (_, useWasm, _, _, remaining) = ExtractDbPathAndOptions(args);
    var (storageBackend, remainingAfterStorage) = ExtractStorageBackend(remaining);
    if (remainingAfterStorage.Count < 1)
    {
        Console.Error.WriteLine("create-db requires <path>");
        return 1;
    }

    var path = Path.GetFullPath(remainingAfterStorage[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    string persistencePath;
    string dbName;
    string parent;

    if (useWasm)
    {
        persistencePath = path.EndsWith(".wasmdb", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.Combine(Path.GetDirectoryName(path) ?? ".", Path.GetFileNameWithoutExtension(path) + ".wasmdb");
        persistencePath = Path.GetFullPath(persistencePath);
        dbName = Path.GetFileNameWithoutExtension(Path.GetFileName(persistencePath));
        parent = ".";
        var fs = new PersistedMemoryFileSystemAccessor(persistencePath);
        var engine = new DatabaseEngine(fs: fs);
        var sql = storageBackend != null ? $"CREATE DATABASE {dbName} WITH (storageBackend={storageBackend})" : $"CREATE DATABASE {dbName}";
        await engine.ExecuteAsync(sql, parent);
        Console.WriteLine($"Created WASM database at {persistencePath}");
    }
    else
    {
        dbName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(dbName))
            dbName = "Database";
        parent = Path.GetDirectoryName(path) ?? ".";
        var engine = new DatabaseEngine();
        var sql = storageBackend != null ? $"CREATE DATABASE {dbName} WITH (storageBackend={storageBackend})" : $"CREATE DATABASE {dbName}";
        await engine.ExecuteAsync(sql, parent);
        Console.WriteLine($"Created database at {path}");
    }
    return 0;
}

static (string? StorageBackend, List<string> Remaining) ExtractStorageBackend(List<string> args)
{
    var list = new List<string>(args);
    string? storageBackend = null;
    var idx = list.FindIndex(a => a.StartsWith("--storage:", StringComparison.OrdinalIgnoreCase));
    if (idx >= 0)
    {
        var val = list[idx]["--storage:".Length..].Trim().ToLowerInvariant();
        if (val is "text" or "binary")
        {
            storageBackend = val;
            list.RemoveAt(idx);
        }
    }
    return (storageBackend, list);
}

static async Task<int> ExecAsync(List<string> args)
{
    var (dbPath, statement) = ParseDbAndArg(args, "exec");
    if (dbPath == null)
        return 1;

    var (_, useWasm, useMemory, persist, _) = ExtractDbPathAndOptions(args);
    var (engine, enginePath, onComplete, _) = await CreateEngineAndPathAsync(dbPath, useWasm, useMemory, persist).ConfigureAwait(false);
    var result = await engine.ExecuteAsync(statement, enginePath).ConfigureAwait(false);
    foreach (var w in result.Warnings ?? [])
        Console.Error.WriteLine($"Warning: {w}");
    if (result.RowsAffected > 0)
        Console.WriteLine($"Rows affected: {result.RowsAffected}");
    if (onComplete != null)
        await onComplete().ConfigureAwait(false);
    return 0;
}

static async Task<int> QueryAsync(List<string> args)
{
    var (dbPath, query) = ParseDbAndArg(args, "query");
    if (dbPath == null)
        return 1;

    var (_, useWasm, useMemory, persist, _) = ExtractDbPathAndOptions(args);
    var (engine, enginePath, onComplete, _) = await CreateEngineAndPathAsync(dbPath, useWasm, useMemory, persist).ConfigureAwait(false);
    var result = await engine.ExecuteQueryAsync(query, enginePath).ConfigureAwait(false);
    if (result.QueryResult == null)
    {
        Console.Error.WriteLine("Expected SELECT query");
        return 1;
    }

    PrintResultGrid(result.QueryResult);
    if (onComplete != null)
        await onComplete().ConfigureAwait(false);
    return 0;
}

static async Task<int> ScriptAsync(List<string> args)
{
    var (dbPath, useWasm, useMemory, persist, remaining) = ExtractDbPathAndOptions(args);
    if (dbPath == null || remaining.Count < 1)
    {
        Console.Error.WriteLine("script requires --db <path> and <file>");
        return 1;
    }

    var filePath = remaining[0];
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"File not found: {filePath}");
        return 1;
    }

    var content = await File.ReadAllTextAsync(filePath);
    var (engine, enginePath, onComplete, _) = await CreateEngineAndPathAsync(dbPath, useWasm, useMemory, persist).ConfigureAwait(false);
    var result = await engine.ExecuteScriptAsync(content, enginePath).ConfigureAwait(false);
    foreach (var qr in result.QueryResults)
        PrintResultGrid(qr);
    foreach (var w in result.Warnings)
        Console.Error.WriteLine($"Warning: {w}");
    Console.WriteLine($"Executed {result.StatementsExecuted} statements");
    if (onComplete != null)
        await onComplete().ConfigureAwait(false);
    return 0;
}

static async Task<int> RebalanceAsync(List<string> args)
{
    var (dbPath, useWasm, useMemory, persist, remaining) = ExtractDbPathAndOptions(args);
    if (dbPath == null)
    {
        Console.Error.WriteLine("rebalance requires --db <path>");
        return 1;
    }

    var tableIdx = remaining.IndexOf("--table");
    if (tableIdx < 0 || tableIdx + 1 >= remaining.Count)
    {
        Console.Error.WriteLine("rebalance requires --table <name>");
        return 1;
    }

    var tableName = remaining[tableIdx + 1];
    var (engine, enginePath, onComplete, _) = await CreateEngineAndPathAsync(dbPath, useWasm, useMemory, persist).ConfigureAwait(false);
    var count = await engine.RebalanceTableAsync(enginePath, tableName).ConfigureAwait(false);
    Console.WriteLine($"Rebalanced table {tableName}: {count} rows processed.");
    if (onComplete != null)
        await onComplete().ConfigureAwait(false);
    return 0;
}

static async Task<int> InspectAsync(List<string> args)
{
    var (dbPath, useWasm, useMemory, persist, _) = ExtractDbPathAndOptions(args);
    if (dbPath == null)
    {
        Console.Error.WriteLine("inspect requires --db <path>");
        return 1;
    }

    var (engine, enginePath, onComplete, fsFromEngine) = await CreateEngineAndPathAsync(dbPath, useWasm, useMemory, persist).ConfigureAwait(false);
    await engine.OpenAsync(enginePath);

    var fs = fsFromEngine ?? (useWasm
        ? (IFileSystemAccessor)new PersistedMemoryFileSystemAccessor(GetWasmPersistencePath(dbPath))
        : new FileSystemAccessor());
    var tablesPath = fs.Combine(enginePath, "Tables");
    if (!fs.DirectoryExists(tablesPath))
    {
        Console.WriteLine("No tables.");
        return 0;
    }

    var schemaStore = new SchemaStore(fs);
    var metadataStore = new MetadataStore(fs);
    var tableNames = await schemaStore.GetTableNamesAsync(enginePath);

    Console.WriteLine($"Database: {(useWasm ? GetWasmPersistencePath(dbPath!) : dbPath)}");
    Console.WriteLine();
    foreach (var tableName in tableNames)
    {
        var schema = await schemaStore.ReadSchemaAsync(enginePath, tableName);
        var (rowCount, activeCount, deletedCount) = await metadataStore.ReadMetadataAsync(enginePath, tableName);

        Console.WriteLine($"Table: {tableName}");
        Console.WriteLine($"  Rows: {activeCount} active, {deletedCount} deleted, {rowCount} total");
        if (schema != null)
        {
            foreach (var col in schema.Columns)
                Console.WriteLine($"  - {col.Name}: {col.Type}" + (col.Width.HasValue ? $"({col.Width})" : ""));
        }
        Console.WriteLine();
    }

    if (onComplete != null)
        await onComplete().ConfigureAwait(false);
    return 0;
}

static (string? DbPath, string Arg) ParseDbAndArg(List<string> args, string cmd)
{
    var (dbPath, _, _, _, remaining) = ExtractDbPathAndOptions(args);
    if (dbPath == null)
        return (null, "");

    if (remaining.Count < 1)
    {
        Console.Error.WriteLine($"{cmd} requires a statement or query argument");
        return (null, "");
    }

    var arg = string.Join(" ", remaining);
    if (arg.StartsWith('"') && arg.EndsWith('"'))
        arg = arg[1..^1];
    return (dbPath, arg);
}

static (string? DbPath, bool UseWasm, bool UseMemory, bool Persist, List<string> Remaining) ExtractDbPathAndOptions(List<string> args)
{
    var list = new List<string>(args);
    string? dbPath = null;
    var useWasm = false;
    var useMemory = false;
    var persist = false;

    var dbIdx = list.IndexOf("--db");
    if (dbIdx >= 0 && dbIdx + 1 < list.Count)
    {
        dbPath = Path.GetFullPath(list[dbIdx + 1]);
        list.RemoveAt(dbIdx + 1);
        list.RemoveAt(dbIdx);
    }

    var wasmIdx = list.IndexOf("--wasm");
    if (wasmIdx >= 0)
    {
        useWasm = true;
        list.RemoveAt(wasmIdx);
    }

    var memoryIdx = list.IndexOf("--memory");
    if (memoryIdx >= 0)
    {
        useMemory = true;
        list.RemoveAt(memoryIdx);
    }

    var persistIdx = list.IndexOf("--persist");
    if (persistIdx >= 0)
    {
        persist = true;
        list.RemoveAt(persistIdx);
    }

    return (dbPath, useWasm, useMemory, persist, list);
}

static async Task<(IDatabaseEngine Engine, string EnginePath, Func<Task>? OnComplete, IFileSystemAccessor? Fs)> CreateEngineAndPathAsync(string dbPath, bool useWasm, bool useMemory, bool persist)
{
    if (useMemory && !useWasm)
    {
        var (memory, virtualRoot) = await DatabaseLoader.LoadIntoMemoryAsync(dbPath).ConfigureAwait(false);
        var engine = new DatabaseEngine(fs: memory);
        Func<Task>? onComplete = null;
        if (persist)
            onComplete = async () => await DatabaseLoader.SaveToDiskAsync(memory, dbPath, virtualRoot).ConfigureAwait(false);
        return (engine, virtualRoot, onComplete, memory);
    }

    if (!useWasm)
        return (new DatabaseEngine(), dbPath, null, null);

    var persistencePath = GetWasmPersistencePath(dbPath);
    var wasmFs = new PersistedMemoryFileSystemAccessor(persistencePath);
    var wasmEngine = new DatabaseEngine(fs: wasmFs);
    var wasmRoot = PersistedMemoryFileSystemAccessor.GetVirtualRootFromPersistencePath(persistencePath);
    return (wasmEngine, wasmRoot, null, wasmFs);
}

static string GetWasmPersistencePath(string path)
{
    var full = Path.GetFullPath(path);
    return full.EndsWith(".wasmdb", StringComparison.OrdinalIgnoreCase)
        ? full
        : Path.Combine(Path.GetDirectoryName(full) ?? ".", Path.GetFileNameWithoutExtension(full) + ".wasmdb");
}

static (IDatabaseEngine Engine, string EnginePath) CreateEngineForBuildSampleWiki(string parentPath)
{
    var fullParent = Path.GetFullPath(parentPath);
    var persistencePath = Path.Combine(fullParent, "WikiDb.wasmdb");
    var fs = new PersistedMemoryFileSystemAccessor(persistencePath);
    return (new DatabaseEngine(fs: fs), ".");
}

static void PrintResultGrid(QueryResult result)
{
    var cols = result.ColumnNames;
    var rows = result.Rows;

    if (cols.Count == 0)
        return;

    var widths = cols.Select((c, i) =>
    {
        var max = c.Length;
        foreach (var row in rows)
        {
            var val = row.GetValue(c) ?? "";
            if (val.Length > max)
                max = Math.Min(val.Length, 50);
        }
        return Math.Min(max + 2, 52);
    }).ToList();

    var headerParts = cols.Select((c, i) => TruncatePad(c, widths[i]));
    Console.WriteLine(string.Join(" | ", headerParts));
    Console.WriteLine(new string('-', headerParts.Sum(p => p.Length) + (cols.Count - 1) * 3));

    foreach (var row in rows)
    {
        var cells = cols.Select((c, i) =>
        {
            var v = row.GetValue(c) ?? "";
            return TruncatePad(v, widths[i]);
        });
        Console.WriteLine(string.Join(" | ", cells));
    }
}

static string TruncatePad(string s, int width)
{
    if (s.Length > width - 1)
        s = s[..Math.Min(width - 4, s.Length)] + "...";
    return s.PadRight(width);
}

static int UnknownCommand(string cmd)
{
    Console.Error.WriteLine($"Unknown command: {cmd}");
    PrintUsage();
    return 1;
}
