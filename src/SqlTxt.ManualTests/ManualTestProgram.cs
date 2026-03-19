using SqlTxt.ManualTests.Compare;
using SqlTxt.ManualTests.Results;
using SqlTxt.ManualTests.Tests;

namespace SqlTxt.ManualTests;

internal readonly record struct ManualTestCommonOptions(
    string DbPath,
    string LogPath,
    bool Verbose,
    string Storage,
    string? CompareWith,
    bool SaveDb,
    bool UserSpecifiedDbDirectory,
    string? DefaultRunDirectoryToDelete);

/// <summary>
/// Manual test driver: workspace defaults under manual-test-artifacts/, optional DB retention.
/// </summary>
internal static class ManualTestProgram
{
    public static async Task<int> RunAsync(string[] args)
    {
        var argsList = args.ToList();
        if (argsList.Count == 0)
        {
            PrintUsage();
            return 1;
        }

        var testName = argsList[0].ToLowerInvariant();
        argsList.RemoveAt(0);

        var repoRoot = TryFindSqlTxtRepositoryRoot() ?? Directory.GetCurrentDirectory();
        var options = ParseCommonOptions(argsList, repoRoot);

        var logDir = Path.GetDirectoryName(Path.GetFullPath(options.LogPath));
        if (!string.IsNullOrEmpty(logDir))
            Directory.CreateDirectory(logDir);

        var exitCode = 1;
        try
        {
            using var logger = new ResultLogger(options.LogPath, options.Verbose);
            logger.Log($"SQL.txt Manual Tests - {testName}");
            logger.Log($"Database path: {options.DbPath}");
            logger.Log($"Storage: {options.Storage}");
            if (options.CompareWith is not null)
                logger.Log($"Compare with: {options.CompareWith}");
            logger.Log($"Log file: {options.LogPath}");
            logger.Log($"Save database on disk: {options.SaveDb}");
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
                else if (options.Storage == "all")
                {
                    var textPath = Path.Combine(options.DbPath, "ManualTest_Text");
                    var binaryPath = Path.Combine(options.DbPath, "ManualTest_Binary");
                    var collected = new List<TestResult>();

                    if (testName is "concurrency" or "all")
                    {
                        collected.Add(await RunConcurrencyAsync(argsList, textPath, logger, "text").ConfigureAwait(false));
                        collected.Add(await RunConcurrencyAsync(argsList, binaryPath, logger, "binary").ConfigureAwait(false));
                        if (options.CompareWith == "localdb")
                            collected.Add(await RunLocalDbConcurrencyAsync(argsList, logger).ConfigureAwait(false));
                    }
                    if (testName is "sharding" or "all")
                    {
                        collected.Add(await RunShardingAsync(argsList, textPath, logger, "text").ConfigureAwait(false));
                        collected.Add(await RunShardingAsync(argsList, binaryPath, logger, "binary").ConfigureAwait(false));
                        if (options.CompareWith == "localdb")
                            collected.Add(await RunLocalDbShardingAsync(argsList, logger).ConfigureAwait(false));
                    }
                    if (testName is "sharding-varchar" or "all")
                    {
                        collected.Add(await RunVarcharShardingAsync(argsList, textPath, logger, "text").ConfigureAwait(false));
                        collected.Add(await RunVarcharShardingAsync(argsList, binaryPath, logger, "binary").ConfigureAwait(false));
                        if (options.CompareWith == "localdb")
                            collected.Add(await RunLocalDbVarcharShardingAsync(argsList, logger).ConfigureAwait(false));
                    }
                    results = collected;
                    if (options.Verbose)
                        foreach (var r in collected)
                            logger.LogResult(r);
                    logger.LogSummaryTable(collected);
                }
                else
                {
                    var result = testName switch
                    {
                        "concurrency" => await RunConcurrencyAsync(argsList, options.DbPath, logger, options.Storage),
                        "sharding" => await RunShardingAsync(argsList, options.DbPath, logger, options.Storage),
                        "sharding-varchar" => await RunVarcharShardingAsync(argsList, options.DbPath, logger, options.Storage),
                        "all" => await RunAllAsync(argsList, options.DbPath, logger, options.Storage),
                        _ => UnknownTest(testName)
                    };
                    results.Add(result);
                    if (options.CompareWith == "localdb")
                    {
                        if (options.Storage == "text")
                        {
                            var binaryPath = Path.Combine(options.DbPath, "ManualTest_Binary_Compare");
                            var binaryResult = testName switch
                            {
                                "concurrency" => await RunConcurrencyAsync(new List<string>(argsList), binaryPath, logger, "binary").ConfigureAwait(false),
                                "sharding" => await RunShardingAsync(new List<string>(argsList), binaryPath, logger, "binary").ConfigureAwait(false),
                                "sharding-varchar" => await RunVarcharShardingAsync(new List<string>(argsList), binaryPath, logger, "binary").ConfigureAwait(false),
                                "all" => await RunAllAsync(new List<string>(argsList), binaryPath, logger, "binary").ConfigureAwait(false),
                                _ => (TestResult?)null
                            };
                            if (binaryResult is not null)
                                results.Add(binaryResult);
                        }
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
                    if (options.Verbose)
                        foreach (var r in results)
                            logger.LogResult(r);
                    logger.LogSummaryTable(results);
                }
            }
            catch (Exception ex)
            {
                logger.Log($"Fatal error: {ex}");
                exitCode = 99;
                return exitCode;
            }

            logger.Log($"Results written to {options.LogPath}");

            var passed = results.Count > 0 && results.All(r => r.Passed);
            exitCode = passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Fatal error before logging started: {ex}");
            exitCode = 99;
        }
        finally
        {
            if (!options.SaveDb)
            {
                if (options.DefaultRunDirectoryToDelete is { } d && Directory.Exists(d))
                {
                    try
                    {
                        Directory.Delete(d, recursive: true);
                    }
                    catch
                    {
                        /* best-effort cleanup */
                    }
                }
                else if (options.UserSpecifiedDbDirectory)
                {
                    TryDeleteKnownManualTestDbTrees(options.DbPath);
                }
            }
        }

        return exitCode;
    }

    private static bool IsSqlTxtRepoRoot(string path) =>
        File.Exists(Path.Combine(path, "SqlTxt.slnx")) ||
        (File.Exists(Path.Combine(path, "Directory.Build.props")) &&
         Directory.Exists(Path.Combine(path, "src", "SqlTxt.Engine")));

    private static string? TryFindSqlTxtRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (IsSqlTxtRepoRoot(dir.FullName))
                return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static void TryDeleteKnownManualTestDbTrees(string dbRoot)
    {
        static void TryDel(string p)
        {
            try
            {
                if (Directory.Exists(p))
                    Directory.Delete(p, recursive: true);
            }
            catch
            {
                /* best-effort */
            }
        }

        TryDel(Path.Combine(dbRoot, "WikiDb"));
        TryDel(Path.Combine(dbRoot, "VarcharShardingDb"));
        TryDel(Path.Combine(dbRoot, "ManualTest_Text"));
        TryDel(Path.Combine(dbRoot, "ManualTest_Binary"));
        TryDel(Path.Combine(dbRoot, "ManualTest_Binary_Compare"));
    }

    private static ManualTestCommonOptions ParseCommonOptions(List<string> args, string repoRoot)
    {
        string? dbPath = null;
        string? logPath = null;
        var verbose = false;
        var storage = "text";
        string? compareWith = null;
        var saveDb = false;
        var userSpecifiedDb = false;

        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] == "--db" && i + 1 < args.Count)
            {
                dbPath = Path.GetFullPath(args[i + 1]);
                userSpecifiedDb = true;
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
            else if (args[i] == "--save-db")
            {
                saveDb = true;
                args.RemoveAt(i);
                i--;
            }
        }

        var artifactsDir = Path.Combine(repoRoot, "manual-test-artifacts");
        string? defaultRunDirectoryToDelete = null;
        if (dbPath is null)
        {
            Directory.CreateDirectory(artifactsDir);
            var runDir = Path.Combine(artifactsDir, $"run-{DateTime.Now:yyyyMMdd-HHmmss}");
            Directory.CreateDirectory(runDir);
            dbPath = runDir;
            defaultRunDirectoryToDelete = runDir;
        }

        logPath ??= Path.Combine(artifactsDir, "logs", $"ManualTests_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        return new ManualTestCommonOptions(
            dbPath,
            logPath,
            verbose,
            storage,
            compareWith,
            saveDb,
            userSpecifiedDb,
            defaultRunDirectoryToDelete);
    }

    private static async Task<TestResult> RunConcurrencyAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
    {
        var threads = 8;
        var ops = 50;
        var readers = 0;
        var insertBatchSize = 1;

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
            else if (args[i] == "--batched-inserts")
            {
                insertBatchSize = Math.Max(insertBatchSize, 10);
            }
            else if (args[i] == "--insert-batch" && i + 1 < args.Count && int.TryParse(args[i + 1], out var b))
            {
                insertBatchSize = Math.Clamp(b, 1, 500);
                i++;
            }
        }

        var storageBackend = storage is "binary" ? "binary" : null;
        logger.Log($"Concurrency test: threads={threads}, ops/thread={ops}, readers={readers}, storage={storage}, insertBatchSize={insertBatchSize}");
        return await HighConcurrencyTest.RunAsync(dbPath, threads, ops, readers, storageBackend, logger, insertBatchSize: insertBatchSize).ConfigureAwait(false);
    }

    private static async Task<TestResult> RunShardingAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
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

    private static async Task<TestResult> RunAllAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
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

    private static async Task<TestResult> RunLocalDbConcurrencyAsync(List<string> args, ResultLogger logger)
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

    private static async Task<TestResult> RunLocalDbShardingAsync(List<string> args, ResultLogger logger)
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

    private static async Task<TestResult> RunLocalDbVarcharShardingAsync(List<string> args, ResultLogger logger)
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

    private static async Task<TestResult> RunVarcharShardingAsync(List<string> args, string dbPath, ResultLogger logger, string storage)
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

    private static TestResult UnknownTest(string name)
    {
        Console.Error.WriteLine($"Unknown test: {name}");
        Console.Error.WriteLine("Valid tests: concurrency, sharding, sharding-varchar, all");
        return new TestResult(name, false, TimeSpan.Zero, 0, 0, 1, new[] { $"Unknown test: {name}" }, null);
    }

    private static void PrintUsage()
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
              --db <path>       Database parent path (default: manual-test-artifacts/run-<timestamp> under repo)
              --log <path>      Log file (default: manual-test-artifacts/logs/ManualTests_<timestamp>.log)
              --storage <type>  text | binary | all (default: text). Use 'all' to compare timings.
              --compare:<db>    Run same test against comparison DB. Use --compare:localdb for SQL Server LocalDB.
              --save-db         Keep database folders after the run (default: delete run dir or WikiDb/VarcharShardingDb/ManualTest_* under --db)
              --verbose        Extra output

            Concurrency options:
              --threads <n>     Writer threads (default: 8)
              --ops <n>         Row operations per thread per insert phase (default: 50)
              --readers <n>     Concurrent SELECT threads with NOLOCK (default: 0)
              --batched-inserts Use multi-row INSERT batches (default batch size 10; less round-trips; worst case remains single-row statements)
              --insert-batch <n> Override batch size (1–500; 1 = one row per INSERT)

            Sharding options:
              --shards <n>   Desired shard count (default: 5)
              --rows <n>     Number of rows to insert (default: 500 for sharding, 200 for sharding-varchar)

            Examples:
              dotnet run --project src/SqlTxt.ManualTests -- concurrency
              dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./manual-test-artifacts/my-run --save-db
              dotnet run --project src/SqlTxt.ManualTests -- sharding --storage all
              dotnet run --project src/SqlTxt.ManualTests -- sharding --compare:localdb
              dotnet run --project src/SqlTxt.ManualTests -- sharding-varchar --rows 200
              dotnet run --project src/SqlTxt.ManualTests -- all --storage all --compare:localdb
            """);
    }
}
