using SqlTxt.ManualTests.Compare;
using SqlTxt.ManualTests.Diagnostics;
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
    string? DefaultRunDirectoryToDelete,
    bool RequireBeatLocalDb,
    bool DiagnosticsEnabled,
    double? FailOnDeficitRatio);

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
            var runId = Guid.NewGuid().ToString("N");
            using var logger = new ResultLogger(options.LogPath, options.Verbose, runId);
            using var diagnosticRun = ManualTestRunContext.TryStart(options.DiagnosticsEnabled, options.LogPath, runId);
            logger.Log($"SQL.txt Manual Tests - {testName}");
            logger.Log($"Database path: {options.DbPath}");
            logger.Log(options.Storage == "all"
                ? "Storage: all (SQL.txt text + binary filesystem, plus LocalDB for every test that has a LocalDB scenario)"
                : $"Storage: {options.Storage}");
            if (options.CompareWith is not null && options.Storage != "all")
                logger.Log($"Compare with: {options.CompareWith} (LocalDB runs in addition to SqlTxt storage above)");
            logger.Log($"Log file: {options.LogPath}");
            if (options.DiagnosticsEnabled && diagnosticRun is not null)
                logger.Log($"Diagnostics JSONL: {diagnosticRun.DiagnosticsJsonlPath}");
            logger.Log($"Save database on disk: {options.SaveDb}");
            logger.Log(string.Empty);

            var results = new List<TestResult>();
            var sqlTxtSlowerThanLocalDb = false;
            var deficitRatioViolation = false;
            try
            {
                if (!IsKnownManualTest(testName))
                {
                    var unknown = UnknownTest(testName);
                    results.Add(unknown);
                    logger.LogResult(unknown);
                }
                else if (testName == "all")
                {
                    results = await RunFullSuiteAsync(argsList, options, logger).ConfigureAwait(false);
                }
                else if (IsPhase4TestName(testName))
                {
                    results = await RunPhase4DriverAsync(testName, argsList, options, logger).ConfigureAwait(false);
                }
                else
                {
                    results = await RunLegacySubtestExpandedAsync(
                        testName,
                        argsList,
                        options.DbPath,
                        logger,
                        options.Storage,
                        options.CompareWith).ConfigureAwait(false);
                }

                if (options.Verbose)
                    foreach (var r in results)
                        logger.LogResult(r);
                results = EnrichResultsWithRunMetadata(results, runId, diagnosticRun?.DiagnosticsJsonlPath);
                logger.LogSummaryTable(results);

                var issuesPath = ManualTestIssuesReport.WriteMarkdown(
                    options.LogPath,
                    results,
                    runId,
                    ManualTestComparator.DefaultSuite,
                    out sqlTxtSlowerThanLocalDb);
                logger.Log($"Secondary report (errors + SqlTxt vs LocalDB): {issuesPath}");
                if (options.RequireBeatLocalDb && sqlTxtSlowerThanLocalDb)
                    logger.Log("FAIL (--require-beat-localdb): at least one passing SqlTxt run was slower than LocalDB for the same test. See #slower-than-localdb in the secondary report.");
                if (options.FailOnDeficitRatio is { } maxRatio
                    && ManualTestIssuesReport.AnyDeficitExceedsRatio(results, maxRatio, ManualTestComparator.DefaultSuite))
                {
                    deficitRatioViolation = true;
                    logger.Log(
                        $"FAIL (--fail-on-deficit-ratio {maxRatio.ToString(System.Globalization.CultureInfo.InvariantCulture)}): SqlTxt text/binary exceeded duration ratio vs baseline. See #deficits in the secondary report.");
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
            var beatOk = !options.RequireBeatLocalDb || !sqlTxtSlowerThanLocalDb;
            var deficitOk = !deficitRatioViolation;
            exitCode = passed && beatOk && deficitOk ? 0 : 1;
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
        TryDel(Path.Combine(dbRoot, "Phase4BindExprDb"));
        TryDel(Path.Combine(dbRoot, "Phase4JoinsDb"));
        TryDel(Path.Combine(dbRoot, "Phase4OrderByDb"));
        TryDel(Path.Combine(dbRoot, "Phase4GroupByDb"));
        TryDel(Path.Combine(dbRoot, "Phase4SubqueriesDb"));
    }

    private static readonly string[] LegacyOrderedSubtests =
    {
        "concurrency", "sharding", "sharding-varchar"
    };

    private static readonly string[] Phase4OrderedSubtests =
    {
        "phase4-bind-expr", "phase4-joins", "phase4-orderby", "phase4-groupby", "phase4-subqueries"
    };

    private static readonly string[] FullSuiteOrderedSubtests =
        LegacyOrderedSubtests.Concat(Phase4OrderedSubtests).ToArray();

    private static bool IsKnownManualTest(string name) =>
        name is "all"
        || IsPhase4TestName(name)
        || LegacyOrderedSubtests.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IsPhase4SingleSubtest(string name) =>
        Phase4OrderedSubtests.Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IncludeLocalDb(string storage, string? compareWith) =>
        storage == "all" || string.Equals(compareWith, "localdb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// SqlTxt filesystem backends to run before optional LocalDB. When <paramref name="storage"/> is <c>all</c>,
    /// uses ManualTest_Text / ManualTest_Binary under <paramref name="dbBase"/>.
    /// </summary>
    private static List<(string Path, string Backend)> SqlTxtFilesystemBackends(string dbBase, string storage, string? compareWith)
    {
        var textPath = Path.Combine(dbBase, "ManualTest_Text");
        var binaryPath = Path.Combine(dbBase, "ManualTest_Binary");
        var binaryComparePath = Path.Combine(dbBase, "ManualTest_Binary_Compare");

        return storage switch
        {
            "all" => [(textPath, "text"), (binaryPath, "binary")],
            "text" when compareWith == "localdb" => [(dbBase, "text"), (binaryComparePath, "binary")],
            "text" => [(dbBase, "text")],
            "binary" when compareWith == "localdb" => [(dbBase, "binary")],
            "binary" => [(dbBase, "binary")],
            _ => [(dbBase, "text")]
        };
    }

    private static async Task<List<TestResult>> RunFullSuiteAsync(
        List<string> args,
        ManualTestCommonOptions options,
        ResultLogger logger)
    {
        var results = new List<TestResult>();
        for (var i = 0; i < FullSuiteOrderedSubtests.Length;)
        {
            var id = FullSuiteOrderedSubtests[i];
            if (IsPhase4SingleSubtest(id))
            {
                var block = new List<string>();
                while (i < FullSuiteOrderedSubtests.Length && IsPhase4SingleSubtest(FullSuiteOrderedSubtests[i]))
                {
                    block.Add(FullSuiteOrderedSubtests[i]);
                    i++;
                }

                results.AddRange(
                    await RunPhase4BlockExpandedAsync(block, options.DbPath, logger, options.Storage, options.CompareWith)
                        .ConfigureAwait(false));
            }
            else
            {
                results.AddRange(
                    await RunLegacySubtestExpandedAsync(id, args, options.DbPath, logger, options.Storage, options.CompareWith)
                        .ConfigureAwait(false));
                i++;
            }
        }

        return results;
    }

    private static Task<List<TestResult>> RunPhase4DriverAsync(
        string testName,
        List<string> args,
        ManualTestCommonOptions options,
        ResultLogger logger)
    {
        var subs = testName == "phase4-all" ? Phase4OrderedSubtests : new[] { testName };
        return RunPhase4BlockExpandedAsync(subs, options.DbPath, logger, options.Storage, options.CompareWith);
    }

    /// <summary>
    /// Runs Phase 4 SqlTxt backends for each subtest, then LocalDB either per subtest or once for the full block when
    /// <paramref name="subs"/> is the complete ordered Phase 4 list and LocalDB is enabled.
    /// </summary>
    private static async Task<List<TestResult>> RunPhase4BlockExpandedAsync(
        IReadOnlyList<string> subs,
        string dbBase,
        ResultLogger logger,
        string storage,
        string? compareWith)
    {
        var results = new List<TestResult>();
        foreach (var sub in subs)
        {
            foreach (var (path, backend) in SqlTxtFilesystemBackends(dbBase, storage, compareWith))
            {
                results.Add(await RunPhase4OneAsync(sub, path, logger, backend).ConfigureAwait(false));
            }
        }

        var singleLocalDb = ShouldRunPhase4LocalDbSingleDatabase(subs, storage, compareWith);

        if (IncludeLocalDb(storage, compareWith) && !singleLocalDb)
        {
            foreach (var sub in subs)
            {
                results.Add(await RunPhase4OneAsync(sub, dbBase, logger, "localdb").ConfigureAwait(false));
            }
        }

        if (singleLocalDb)
        {
            results.AddRange(await LocalDbComparisons.RunPhase4SuiteSingleDatabaseAsync(logger).ConfigureAwait(false));
        }

        return results;
    }

    private static bool ShouldRunPhase4LocalDbSingleDatabase(
        IReadOnlyList<string> subs,
        string storage,
        string? compareWith)
    {
        if (!IncludeLocalDb(storage, compareWith) || subs.Count != Phase4OrderedSubtests.Length)
            return false;
        return subs.SequenceEqual(Phase4OrderedSubtests, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<List<TestResult>> RunLegacySubtestExpandedAsync(
        string testId,
        List<string> args,
        string dbBase,
        ResultLogger logger,
        string storage,
        string? compareWith)
    {
        var results = new List<TestResult>();
        foreach (var (path, backend) in SqlTxtFilesystemBackends(dbBase, storage, compareWith))
        {
            var r = testId switch
            {
                "concurrency" => await RunConcurrencyAsync(new List<string>(args), path, logger, backend).ConfigureAwait(false),
                "sharding" => await RunShardingAsync(new List<string>(args), path, logger, backend).ConfigureAwait(false),
                "sharding-varchar" => await RunVarcharShardingAsync(new List<string>(args), path, logger, backend)
                    .ConfigureAwait(false),
                _ => UnknownTest(testId)
            };
            results.Add(r);
        }

        if (IncludeLocalDb(storage, compareWith))
        {
            var lr = testId switch
            {
                "concurrency" => await RunLocalDbConcurrencyAsync(args, logger).ConfigureAwait(false),
                "sharding" => await RunLocalDbShardingAsync(args, logger).ConfigureAwait(false),
                "sharding-varchar" => await RunLocalDbVarcharShardingAsync(args, logger).ConfigureAwait(false),
                _ => UnknownTest(testId)
            };
            results.Add(lr);
        }

        return results;
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
        var requireBeatLocalDb = false;
        var diagnosticsEnabled = false;
        double? failOnDeficitRatio = null;

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
            else if (args[i] == "--require-beat-localdb")
            {
                requireBeatLocalDb = true;
                args.RemoveAt(i);
                i--;
            }
            else if (args[i] == "--diagnostics")
            {
                diagnosticsEnabled = true;
                args.RemoveAt(i);
                i--;
            }
            else if (args[i] == "--fail-on-deficit-ratio" && i + 1 < args.Count
                     && double.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ratio)
                     && ratio > 0)
            {
                failOnDeficitRatio = ratio;
                args.RemoveAt(i);
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
            defaultRunDirectoryToDelete,
            requireBeatLocalDb,
            diagnosticsEnabled,
            failOnDeficitRatio);
    }

    private static List<TestResult> EnrichResultsWithRunMetadata(
        IReadOnlyList<TestResult> results,
        string runId,
        string? diagnosticsJsonlPath) =>
        results.Select(r => r with { RunId = runId, DiagnosticsJsonlPath = diagnosticsJsonlPath }).ToList();

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

    private static bool IsPhase4TestName(string name) =>
        name is "phase4-bind-expr" or "phase4-joins" or "phase4-orderby" or "phase4-groupby" or "phase4-subqueries" or "phase4-all";

    private static Task<TestResult> RunPhase4OneAsync(string testName, string dbPath, ResultLogger logger, string storage)
    {
        if (storage == "localdb")
        {
            return testName switch
            {
                "phase4-bind-expr" => LocalDbComparisons.RunPhase4BindExprAsync(logger),
                "phase4-joins" => LocalDbComparisons.RunPhase4JoinsAsync(logger),
                "phase4-orderby" => LocalDbComparisons.RunPhase4OrderByAsync(logger),
                "phase4-groupby" => LocalDbComparisons.RunPhase4GroupByAsync(logger),
                "phase4-subqueries" => LocalDbComparisons.RunPhase4SubqueriesAsync(logger),
                _ => Task.FromResult(UnknownTest(testName))
            };
        }

        var backend = storage is "binary" ? "binary" : null;
        return testName switch
        {
            "phase4-bind-expr" => Phase4BindExprManualTest.RunAsync(dbPath, backend, logger),
            "phase4-joins" => Phase4JoinsManualTest.RunAsync(dbPath, backend, logger),
            "phase4-orderby" => Phase4OrderByManualTest.RunAsync(dbPath, backend, logger),
            "phase4-groupby" => Phase4GroupByManualTest.RunAsync(dbPath, backend, logger),
            "phase4-subqueries" => Phase4SubqueriesManualTest.RunAsync(dbPath, backend, logger),
            _ => Task.FromResult(UnknownTest(testName))
        };
    }

    private static TestResult UnknownTest(string name)
    {
        Console.Error.WriteLine($"Unknown test: {name}");
        Console.Error.WriteLine(
            "Valid tests: concurrency, sharding, sharding-varchar, all, phase4-bind-expr, phase4-joins, phase4-orderby, phase4-groupby, phase4-subqueries, phase4-all");
        return new TestResult(name, false, TimeSpan.Zero, 0, 0, 1, new[] { $"Unknown test: {name}" }, null);
    }

    private static void PrintUsage()
    {
            Console.WriteLine("""
            SQL.txt Manual Tests - Concurrency, sharding, Phase 4 query features, performance

            Usage:
              dotnet run --project src/SqlTxt.ManualTests -- <test> [options]

            Tests:
              concurrency       High concurrency: multi-thread INSERT/UPDATE/DELETE
              sharding          Sharding: insert many rows (fixed-width), measure query speed
              sharding-varchar  Sharding: insert many rows (VARCHAR), verify rebalance
              all               Full suite: legacy tests above + every Phase 4 subtest (ordered)
              phase4-bind-expr  Phase 4.1: compound WHERE / expression binding (see docs/plans/Phase4_01_*.md)
              phase4-joins      Phase 4.2: INNER/LEFT JOIN (Phase4_02_Joins_Execution_Plan.md)
              phase4-orderby    Phase 4.3: ORDER BY (Phase4_03_OrderBy_Sort_Plan.md)
              phase4-groupby    Phase 4.4: GROUP BY / aggregates / HAVING (Phase4_04_*.md)
              phase4-subqueries Phase 4.5: IN / EXISTS / scalar subqueries (Phase4_05_*.md)
              phase4-all        Run all Phase 4 manual tests in order

            Common options:
              --db <path>       Database parent path (default: manual-test-artifacts/run-<timestamp> under repo)
              --log <path>      Log file (default: manual-test-artifacts/logs/ManualTests_<timestamp>.log)
              --storage <type>  text | binary | all (default: text).
                                'all' = run SQL.txt text + binary (separate dirs) AND LocalDB for every scenario that defines a LocalDB runner.
              --compare:<db>    Optional when storage is text or binary: add --compare:localdb to also run the LocalDB scenario.
                                Redundant with --storage all (LocalDB already included; no duplicate LocalDB runs).
              --save-db         Keep database folders after the run (default: delete run dir or WikiDb/VarcharShardingDb/ManualTest_* under --db)
              --verbose        Extra output
              --require-beat-localdb  Fail exit code 1 if any passing SqlTxt (text/binary) run is slower than LocalDB for the same test (see secondary .md report)
              --diagnostics    Emit structured JSON Lines trace next to the log (correlate via RunId in log and TestResult rows)
              --fail-on-deficit-ratio <r>  Fail exit code 1 if any SqlTxt vs baseline duration ratio exceeds r (see #deficits in the secondary .md report; r must be > 0)

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
              dotnet run --project src/SqlTxt.ManualTests -- all --storage all
              dotnet run --project src/SqlTxt.ManualTests -- phase4-all --storage all
            """);
    }
}
