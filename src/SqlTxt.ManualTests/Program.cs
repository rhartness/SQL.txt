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

var (dbPath, logPath, verbose) = ParseCommonOptions(argsList);

using var logger = new ResultLogger(logPath, verbose);
logger.Log($"SQL.txt Manual Tests - {testName}");
logger.Log($"Database path: {dbPath}");
logger.Log($"Log file: {logPath}");
logger.Log(string.Empty);

TestResult result;
try
{
    result = testName switch
    {
        "concurrency" => await RunConcurrencyAsync(argsList, dbPath, logger),
        "sharding" => await RunShardingAsync(argsList, dbPath, logger),
        "all" => await RunAllAsync(argsList, dbPath, logger),
        _ => UnknownTest(testName)
    };
}
catch (Exception ex)
{
    logger.Log($"Fatal error: {ex}");
    return 99;
}

logger.LogResult(result);
logger.Log($"Results written to {logPath}");

return result.Passed ? 0 : 1;

static (string DbPath, string LogPath, bool Verbose) ParseCommonOptions(List<string> args)
{
    var dbPath = Path.GetFullPath(".");
    var logPath = Path.Combine(Directory.GetCurrentDirectory(), $"ManualTests_{DateTime.Now:yyyyMMdd_HHmmss}.log");
    var verbose = false;

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
        else if (args[i] == "--verbose")
        {
            verbose = true;
            args.RemoveAt(i);
            i--;
        }
    }

    return (dbPath, logPath, verbose);
}

static async Task<TestResult> RunConcurrencyAsync(List<string> args, string dbPath, ResultLogger logger)
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

    logger.Log($"Concurrency test: threads={threads}, ops/thread={ops}, readers={readers}");
    return await HighConcurrencyTest.RunAsync(dbPath, threads, ops, readers, logger).ConfigureAwait(false);
}

static async Task<TestResult> RunShardingAsync(List<string> args, string dbPath, ResultLogger logger)
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

    logger.Log($"Sharding test: desired shards={shards}, rows={rows}");
    return await ShardingTest.RunAsync(dbPath, shards, rows, logger).ConfigureAwait(false);
}

static async Task<TestResult> RunAllAsync(List<string> args, string dbPath, ResultLogger logger)
{
    var concurrencyResult = await RunConcurrencyAsync(new List<string>(args), dbPath, logger).ConfigureAwait(false);
    var shardingResult = await RunShardingAsync(new List<string>(args), dbPath, logger).ConfigureAwait(false);

    var passed = concurrencyResult.Passed && shardingResult.Passed;
    return new TestResult(
        "All",
        passed,
        concurrencyResult.Duration + shardingResult.Duration,
        concurrencyResult.OperationsCount + shardingResult.OperationsCount,
        concurrencyResult.SuccessCount + shardingResult.SuccessCount,
        concurrencyResult.FailureCount + shardingResult.FailureCount,
        concurrencyResult.Exceptions.Concat(shardingResult.Exceptions).ToList(),
        null);
}

static TestResult UnknownTest(string name)
{
    Console.Error.WriteLine($"Unknown test: {name}");
    Console.Error.WriteLine("Valid tests: concurrency, sharding, all");
    return new TestResult(name, false, TimeSpan.Zero, 0, 0, 1, new[] { $"Unknown test: {name}" }, null);
}

static void PrintUsage()
{
    Console.WriteLine("""
        SQL.txt Manual Tests - Concurrency, sharding, and performance testing

        Usage:
          dotnet run --project src/SqlTxt.ManualTests -- <test> [options]

        Tests:
          concurrency    High concurrency: multi-thread INSERT/UPDATE/DELETE
          sharding       Sharding: insert many rows, measure query speed
          all            Run both tests

        Common options:
          --db <path>    Database path (default: current directory)
          --log <path>   Log file path (default: ManualTests_<timestamp>.log)
          --verbose     Extra output

        Concurrency options:
          --threads <n>  Writer threads (default: 8)
          --ops <n>      Operations per thread (default: 50)
          --readers <n>  Concurrent SELECT threads with NOLOCK (default: 0)

        Sharding options:
          --shards <n>   Desired shard count for Page table (default: 5)
          --rows <n>     Number of Page rows to insert (default: 500)

        Examples:
          dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb
          dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --shards 5 --rows 500
        """);
}
