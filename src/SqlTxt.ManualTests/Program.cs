using SqlTxt.ManualTests.Compare;
using SqlTxt.ManualTests.Results;
using SqlTxt.ManualTests.Tests;

var argsList = args.ToList();
if (argsList.Count == 0)
{
    PrintUsage();
    return 1;
}

var testName = argsList[0].ToLowerInvariant();
argsList.RemoveAt(0);

var (dbPath, logPath, verbose, storage, compareWith) = ParseCommonOptions(argsList);

using var logger = new ResultLogger(logPath, verbose);
logger.Log($"SQL.txt Manual Tests - {testName}");
logger.Log($"Database path: {dbPath}");
logger.Log($"Storage: {storage}");
if (compareWith is not null)
    logger.Log($"Compare with: {compareWith}");
logger.Log($"Log file: {logPath}");
logger.Log(string.Empty);

var results = new List<TestResult>();
try
{
    var isValidTest = testName is "concurrency" or "sharding" or "sharding-varchar" or "all";
    if (!isValidTest)
    {
        var unknown = UnknownTest(testName);
        results.Add(unknown);
        logger.LogResult(unknown);
    }
    else if (storage == "all")
    {
        var textPath = Path.Combine(dbPath, "ManualTest_Text");
        var binaryPath = Path.Combine(dbPath, "ManualTest_Binary");
        var collected = new List<TestResult>();

        if (testName is "concurrency" or "all")
        {
            collected.Add(await RunConcurrencyAsync(argsList, textPath, logger, "text").ConfigureAwait(false));
            collected.Add(await RunConcurrencyAsync(argsList, binaryPath, logger, "binary").ConfigureAwait(false));
            if (compareWith == "localdb")
                collected.Add(await RunLocalDbConcurrencyAsync(argsList, logger).ConfigureAwait(false));
        }
        if (testName is "sharding" or "all")
        {
            collected.Add(await RunShardingAsync(argsList, textPath, logger, "text").ConfigureAwait(false));
            collected.Add(await RunShardingAsync(argsList, binaryPath, logger, "binary").ConfigureAwait(false));
            if (compareWith == "localdb")
                collected.Add(await RunLocalDbShardingAsync(argsList, logger).ConfigureAwait(false));
        }
        if (testName is "sharding-varchar" or "all")
        {
            collected.Add(await RunVarcharShardingAsync(argsList, textPath, logger, "text").ConfigureAwait(false));
            collected.Add(await RunVarcharShardingAsync(argsList, binaryPath, logger, "binary").ConfigureAwait(false));
            if (compareWith == "localdb")
                collected.Add(await RunLocalDbVarcharShardingAsync(argsList, logger).ConfigureAwait(false));
        }
        results = collected;
        if (verbose)
            foreach (var r in collected)
                logger.LogResult(r);
        logger.LogSummaryTable(collected);
    }
    else
    {
        var result = testName switch
        {
            "concurrency" => await RunConcurrencyAsync(argsList, dbPath, logger, storage),
            "sharding" => await RunShardingAsync(argsList, dbPath, logger, storage),
            "sharding-varchar" => await RunVarcharShardingAsync(argsList, dbPath, logger, storage),
            "all" => await RunAllAsync(argsList, dbPath, logger, storage),
            _ => UnknownTest(testName)
        };
        results.Add(result);
        if (compareWith == "localdb")
        {
            if (testName == "all")
            {
                results.Add(await RunLocalDbConcurrencyAsync(argsList, logger).ConfigureAwait(false));
                results.Add(await RunLocalDbShardingAsync(argsList, logger).ConfigureAwait(false));
                results.Add(await RunLocalDbVarcharShardingAsync(argsList, logger).ConfigureAwait(false));
            }
            else
            {
                var localDbResult = testName switch
                {
                    "concurrency" => await RunLocalDbConcurrencyAsync(argsList, logger).ConfigureAwait(false),
                    "sharding" => await RunLocalDbShardingAsync(argsList, logger).ConfigureAwait(false),
                    "sharding-varchar" => await RunLocalDbVarcharShardingAsync(argsList, logger).ConfigureAwait(false),
                    _ => (TestResult?)null
                };
                if (localDbResult is not null)
                    results.Add(localDbResult);
            }
        }
        if (verbose)
            foreach (var r in results)
                logger.LogResult(r);
        logger.LogSummaryTable(results);
    }
}
catch (Exception ex)
{
    logger.Log($"Fatal error: {ex}");
    return 99;
}

logger.Log($"Results written to {logPath}");

var passed = results.Count > 0 && results.All(r => r.Passed);
return passed ? 0 : 1;

static (string DbPath, string LogPath, bool Verbose, string Storage, string? CompareWith) ParseCommonOptions(List<string> args)
{
    var dbPath = Path.GetFullPath(".");
    var logPath = Path.Combine(Directory.GetCurrentDirectory(), $"ManualTests_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    var verbose = false;
    var storage = "text";
    string? compareWith = null;

    for (var i = 0; i < args.Count; i++)
    {
        if (args[i] == "--db" && i + 1 < args.Count)
        {
            dbPath = Path.GetFullPath(args[i + 1]);
            args.RemoveAt(i);
            args.RemoveAt(i);
            i--;
        }
        else if (args[i] == "--log" && i + 1 < args.Count)
        {
            logPath = Path.GetFullPath(args[i + 1]);
            args.RemoveAt(i);
            args.RemoveAt(i);
            i--;
        }
        else if (args[i] == "--storage" && i + 1 < args.Count)
        {
            var v = args[i + 1].ToLowerInvariant();
            storage = v is "text" or "binary" or "all" ? v : "text";
            args.RemoveAt(i);
            args.RemoveAt(i);
            i--;
        }
        else if (args[i].StartsWith("--compare:", StringComparison.OrdinalIgnoreCase))
        {
            var value = args[i]["--compare:".Length..].Trim().ToLowerInvariant();
            compareWith = value is "localdb" ? "localdb" : null;
            if (compareWith is null && !string.IsNullOrEmpty(value))
                Console.Error.WriteLine($"Unknown compare backend: {value}. Use --compare:localdb.");
            args.RemoveAt(i);
            i--;
        }
        else if (args[i] == "--verbose")
        {
            verbose = true;
            args.RemoveAt(i);
            i--;
        }
    }

    return (dbPath, logPath, verbose, storage, compareWith);
}

static async Task<TestResult> RunConcurrencyAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
{
    var threads = 8;
    var ops = 50;
    var readers = 0;

    for (var i = 0; i < args.Count; i++)
    {
        if (args[i] == "--threads" && i + 1 < args.Count && int.TryParse(args[i + 1], out var t))
        {
            threads = Math.Max(1, t);
            i++;
        }
        else if (args[i] == "--ops" && i + 1 < args.Count && int.TryParse(args[i + 1], out var o))
        {
            ops = Math.Max(1, o);
            i++;
        }
        else if (args[i] == "--readers" && i + 1 < args.Count && int.TryParse(args[i + 1], out var r))
        {
            readers = Math.Max(0, r);
            i++;
        }
    }

    var storageBackend = storage is "binary" ? "binary" : null;
    logger.Log($"Concurrency test: threads={threads}, ops/thread={ops}, readers={readers}, storage={storage}");
    return await HighConcurrencyTest.RunAsync(dbPath, threads, ops, readers, storageBackend, logger).ConfigureAwait(false);
}

static async Task<TestResult> RunShardingAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
{
    var shards = 5;
    var rows = 500;

    for (var i = 0; i < args.Count; i++)
    {
        if (args[i] == "--shards" && i + 1 < args.Count && int.TryParse(args[i + 1], out var s))
        {
            shards = Math.Max(1, s);
            i++;
        }
        else if (args[i] == "--rows" && i + 1 < args.Count && int.TryParse(args[i + 1], out var r))
        {
            rows = Math.Max(1, r);
            i++;
        }
    }

    var storageBackend = storage is "binary" ? "binary" : null;
    logger.Log($"Sharding test: desired shards={shards}, rows={rows}, storage={storage}");
    return await ShardingTest.RunAsync(dbPath, shards, rows, storageBackend, logger).ConfigureAwait(false);
}

static async Task<TestResult> RunAllAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
{
    var concurrencyResult = await RunConcurrencyAsync(new List<string>(args), dbPath, logger, storage).ConfigureAwait(false);
    var shardingResult = await RunShardingAsync(new List<string>(args), dbPath, logger, storage).ConfigureAwait(false);
    var varcharShardingResult = await RunVarcharShardingAsync(new List<string>(args), dbPath, logger, storage).ConfigureAwait(false);

    var passed = concurrencyResult.Passed && shardingResult.Passed && varcharShardingResult.Passed;
    return new TestResult(
        "All",
        passed,
        concurrencyResult.Duration + shardingResult.Duration + varcharShardingResult.Duration,
        concurrencyResult.OperationsCount + shardingResult.OperationsCount + varcharShardingResult.OperationsCount,
        concurrencyResult.SuccessCount + shardingResult.SuccessCount + varcharShardingResult.SuccessCount,
        concurrencyResult.FailureCount + shardingResult.FailureCount + varcharShardingResult.FailureCount,
        concurrencyResult.Exceptions.Concat(shardingResult.Exceptions).Concat(varcharShardingResult.Exceptions).ToList(),
        null,
        storage is "binary" ? "binary" : null);
}

static async Task<TestResult> RunLocalDbConcurrencyAsync(List<string> args, ResultLogger logger)
{
    var threads = 8;
    var ops = 50;
    var readers = 0;
    for (var i = 0; i < args.Count; i++)
    {
        if (args[i] == "--threads" && i + 1 < args.Count && int.TryParse(args[i + 1], out var t)) { threads = Math.Max(1, t); i++; }
        else if (args[i] == "--ops" && i + 1 < args.Count && int.TryParse(args[i + 1], out var o)) { ops = Math.Max(1, o); i++; }
        else if (args[i] == "--readers" && i + 1 < args.Count && int.TryParse(args[i + 1], out var r)) { readers = Math.Max(0, r); i++; }
    }
    logger.Log($"LocalDB comparison: concurrency (threads={threads}, ops={ops}, readers={readers})");
    return await LocalDbComparisons.RunConcurrencyAsync(threads, ops, readers, logger).ConfigureAwait(false);
}

static async Task<TestResult> RunLocalDbShardingAsync(List<string> args, ResultLogger logger)
{
    var shards = 5;
    var rows = 500;
    for (var i = 0; i < args.Count; i++)
    {
        if (args[i] == "--shards" && i + 1 < args.Count && int.TryParse(args[i + 1], out var s)) { shards = Math.Max(1, s); i++; }
        else if (args[i] == "--rows" && i + 1 < args.Count && int.TryParse(args[i + 1], out var r)) { rows = Math.Max(1, r); i++; }
    }
    logger.Log($"LocalDB comparison: sharding (rows={rows})");
    return await LocalDbComparisons.RunShardingAsync(shards, rows, logger).ConfigureAwait(false);
}

static async Task<TestResult> RunLocalDbVarcharShardingAsync(List<string> args, ResultLogger logger)
{
    var shards = 5;
    var rows = 200;
    for (var i = 0; i < args.Count; i++)
    {
        if (args[i] == "--shards" && i + 1 < args.Count && int.TryParse(args[i + 1], out var s)) { shards = Math.Max(1, s); i++; }
        else if (args[i] == "--rows" && i + 1 < args.Count && int.TryParse(args[i + 1], out var r)) { rows = Math.Max(1, r); i++; }
    }
    logger.Log($"LocalDB comparison: sharding-varchar (rows={rows})");
    return await LocalDbComparisons.RunVarcharShardingAsync(shards, rows, logger).ConfigureAwait(false);
}

static async Task<TestResult> RunVarcharShardingAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
{
    var shards = 5;
    var rows = 200;

    for (var i = 0; i < args.Count; i++)
    {
        if (args[i] == "--shards" && i + 1 < args.Count && int.TryParse(args[i + 1], out var s))
        {
            shards = Math.Max(1, s);
            i++;
        }
        else if (args[i] == "--rows" && i + 1 < args.Count && int.TryParse(args[i + 1], out var r))
        {
            rows = Math.Max(1, r);
            i++;
        }
    }

    var storageBackend = storage is "binary" ? "binary" : null;
    logger.Log($"Sharding (VARCHAR) test: desired shards={shards}, rows={rows}, storage={storage}");
    return await VarcharShardingTest.RunAsync(dbPath, shards, rows, storageBackend, logger).ConfigureAwait(false);
}

static TestResult UnknownTest(string name)
{
    Console.Error.WriteLine($"Unknown test: {name}");
    Console.Error.WriteLine("Valid tests: concurrency, sharding, sharding-varchar, all");
    return new TestResult(name, false, TimeSpan.Zero, 0, 0, 1, new[] { $"Unknown test: {name}" }, null);
}

static void PrintUsage()
{
    Console.WriteLine("""
        SQL.txt Manual Tests - Concurrency, sharding, and performance testing

        Usage:
          dotnet run --project src/SqlTxt.ManualTests -- <test> [options]

        Tests:
          concurrency       High concurrency: multi-thread INSERT/UPDATE/DELETE
          sharding          Sharding: insert many rows (fixed-width), measure query speed
          sharding-varchar  Sharding: insert many rows (VARCHAR), verify rebalance
          all               Run all tests

        Common options:
          --db <path>       Database path (default: current directory)
          --log <path>      Log file path (default: ManualTests_<timestamp>.log)
          --storage <type>  text | binary | all (default: text). Use 'all' to compare timings.
          --compare:<db>    Run same test against comparison DB. Use --compare:localdb for SQL Server LocalDB.
          --verbose        Extra output

        Concurrency options:
          --threads <n>  Writer threads (default: 8)
          --ops <n>      Operations per thread (default: 50)
          --readers <n>  Concurrent SELECT threads with NOLOCK (default: 0)

        Sharding options:
          --shards <n>   Desired shard count (default: 5)
          --rows <n>     Number of rows to insert (default: 500 for sharding, 200 for sharding-varchar)

        Examples:
          dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb
          dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --storage all
          dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --compare:localdb
          dotnet run --project src/SqlTxt.ManualTests -- sharding-varchar --db ./TestDb --rows 200
          dotnet run --project src/SqlTxt.ManualTests -- all --db ./TestDb --storage all --compare:localdb --log ./results.log
        """);
}
