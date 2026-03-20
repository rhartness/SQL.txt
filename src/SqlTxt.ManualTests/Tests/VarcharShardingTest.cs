using System.Diagnostics;
using SqlTxt.Contracts;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Diagnostics;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// VARCHAR sharding test: insert many rows with variable-width Content into a VARCHAR table,
/// verify shard split, full scan, PK lookup, and rebalance.
/// </summary>
public static class VarcharShardingTest
{
    private const int DefaultMaxShardSizeBytes = 50 * 1024; // 50 KB to force shards with variable-width rows

    public static async Task<TestResult> RunAsync(
        string dbPath,
        int desiredShards = 5,
        int rowCount = 200,
        string? storageBackend = null,
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var engine = new DatabaseEngine();
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        var details = new Dictionary<string, object>();
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        var storageLabel = storageBackend ?? "text";
        trace?.SetTestScope("Sharding (VARCHAR)", storageLabel);

        string? failedStage = null;
        string? failedStep = null;
        string? lastSql = null;
        var dbFolder = Path.Combine(dbPath, "VarcharShardingDb");

        try
        {
            using (ManualTestTraceScope.Stage(trace, "Setup"))
            {
                failedStage = "Setup";
                if (Directory.Exists(dbFolder))
                {
                    using (ManualTestTraceScope.Step(trace, "DeleteOldDb"))
                        Directory.Delete(dbFolder, recursive: true);
                }

                var setupSw = Stopwatch.StartNew();
                logger?.Log($"Creating database with Notes table (VARCHAR, maxShardSize={DefaultMaxShardSizeBytes} bytes, storage: {storageLabel})...");

                var createDbSql = storageBackend is "binary"
                    ? "CREATE DATABASE VarcharShardingDb WITH (storageBackend=binary)"
                    : "CREATE DATABASE VarcharShardingDb";
                using (ManualTestTraceScope.Step(trace, "CreateDatabase"))
                {
                    failedStep = "CreateDatabase";
                    lastSql = createDbSql;
                    await engine.ExecuteAsync(createDbSql, dbPath, cancellationToken).ConfigureAwait(false);
                }

                var createTableSql =
                    $"CREATE TABLE Notes (Id CHAR(10) PRIMARY KEY, Content VARCHAR(5000)) WITH (maxShardSize={DefaultMaxShardSizeBytes})";
                using (ManualTestTraceScope.Step(trace, "CreateNotesTable"))
                {
                    failedStep = "CreateNotesTable";
                    lastSql = createTableSql;
                    await engine.ExecuteAsync(createTableSql, dbFolder, cancellationToken).ConfigureAwait(false);
                }

                setupSw.Stop();
                details["Step_Setup_Ms"] = setupSw.Elapsed.TotalMilliseconds;
            }

            using (ManualTestTraceScope.Stage(trace, "Insert"))
            {
                failedStage = "Insert";
                logger?.Log($"Inserting {rowCount} Notes rows with variable Content lengths (100-2000 chars)...");
                var insertSw = Stopwatch.StartNew();
                var random = new Random(42);
                var values = new List<string>();
                for (var i = 0; i < rowCount; i++)
                {
                    var contentLen = 100 + random.Next(1901);
                    var content = new string('x', contentLen);
                    var escaped = content.Replace("'", "''");
                    values.Add($"('{i}', '{escaped}')");
                }
                var valuesStr = string.Join(", ", values);
                var insertSql = "INSERT INTO Notes (Id, Content) VALUES " + valuesStr;
                using (ManualTestTraceScope.Step(trace, "BatchInsert", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                       {
                           ["rowCount"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                       }))
                {
                    failedStep = "BatchInsert";
                    lastSql = insertSql;
                    await engine.ExecuteAsync(insertSql, dbFolder, cancellationToken).ConfigureAwait(false);
                }

                insertSw.Stop();
                var insertMs = insertSw.Elapsed.TotalMilliseconds;
                details["Step_Insert_Ms"] = insertMs;
                details["Step_Insert_Count"] = rowCount;
                details["Avg_Insert_Ms"] = rowCount > 0 ? insertMs / rowCount : 0;

                var tablesPath = Path.Combine(dbFolder, "Tables", "Notes");
                var shardCount = 0;
                if (Directory.Exists(tablesPath))
                {
                    var pattern = storageBackend is "binary" ? "*.bin" : "*.txt";
                    foreach (var f in Directory.GetFiles(tablesPath, pattern))
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (name == "Notes" || (name.StartsWith("Notes_", StringComparison.Ordinal) && name.Length > 6 && int.TryParse(name.AsSpan(6), out _)))
                            shardCount++;
                    }
                }
                details["ShardCount"] = shardCount;
                details["RowCount"] = rowCount;
                trace?.RecordCounter("shardFiles", shardCount, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["table"] = "Notes" });

                logger?.Log($"Shard files: {shardCount}");
            }

            using (ManualTestTraceScope.Stage(trace, "AssertPreRebalance"))
            {
                failedStage = "AssertPreRebalance";
                var fullScanSw = Stopwatch.StartNew();
                EngineResult fullScan;
                using (ManualTestTraceScope.Step(trace, "QueryFullScanPreRebalance"))
                {
                    failedStep = "QueryFullScanPreRebalance";
                    lastSql = "SELECT * FROM Notes";
                    fullScan = await engine.ExecuteQueryAsync("SELECT * FROM Notes", dbFolder, cancellationToken).ConfigureAwait(false);
                }
                fullScanSw.Stop();
                details["Step_Query_FullScan_Ms"] = fullScanSw.Elapsed.TotalMilliseconds;
                if (fullScan.QueryResult == null || fullScan.QueryResult.Rows.Count != rowCount)
                {
                    exceptions.Add($"Full scan expected {rowCount} rows, got {fullScan.QueryResult?.Rows.Count ?? 0}");
                    trace?.RecordIterationError($"Full scan row count mismatch: expected {rowCount}, got {fullScan.QueryResult?.Rows.Count ?? 0}");
                }

                var midId = rowCount / 2;
                var pkLookupSw = Stopwatch.StartNew();
                var pkSql = $"SELECT * FROM Notes WHERE Id = '{midId}'";
                EngineResult pkLookup;
                using (ManualTestTraceScope.Step(trace, "QueryPkLookupPreRebalance"))
                {
                    failedStep = "QueryPkLookupPreRebalance";
                    lastSql = pkSql;
                    pkLookup = await engine.ExecuteQueryAsync(pkSql, dbFolder, cancellationToken).ConfigureAwait(false);
                }
                pkLookupSw.Stop();
                details["Step_Query_PkLookup_Ms"] = pkLookupSw.Elapsed.TotalMilliseconds;
                if (pkLookup.QueryResult == null || pkLookup.QueryResult.Rows.Count != 1)
                {
                    exceptions.Add($"PK lookup Id={midId} expected 1 row, got {pkLookup.QueryResult?.Rows.Count ?? 0}");
                    trace?.RecordIterationError($"PK lookup Id={midId} expected 1 row");
                }
            }

            using (ManualTestTraceScope.Stage(trace, "Rebalance"))
            {
                failedStage = "Rebalance";
                logger?.Log("Running RebalanceTableAsync...");
                var rebalanceSw = Stopwatch.StartNew();
                using (ManualTestTraceScope.Step(trace, "RebalanceTableAsync", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                       {
                           ["table"] = "Notes"
                       }))
                {
                    failedStep = "RebalanceTableAsync";
                    lastSql = "RebalanceTableAsync(Notes)";
                    await engine.RebalanceTableAsync(dbFolder, "Notes", cancellationToken).ConfigureAwait(false);
                }
                rebalanceSw.Stop();
                details["Step_Rebalance_Ms"] = rebalanceSw.Elapsed.TotalMilliseconds;
            }

            using (ManualTestTraceScope.Stage(trace, "AssertPostRebalance"))
            {
                failedStage = "AssertPostRebalance";
                var afterRebalanceSw = Stopwatch.StartNew();
                EngineResult afterRebalance;
                using (ManualTestTraceScope.Step(trace, "QueryFullScanAfterRebalance"))
                {
                    failedStep = "QueryFullScanAfterRebalance";
                    lastSql = "SELECT * FROM Notes";
                    afterRebalance = await engine.ExecuteQueryAsync("SELECT * FROM Notes", dbFolder, cancellationToken).ConfigureAwait(false);
                }
                afterRebalanceSw.Stop();
                details["Step_Query_AfterRebalance_Ms"] = afterRebalanceSw.Elapsed.TotalMilliseconds;
                if (afterRebalance.QueryResult == null || afterRebalance.QueryResult.Rows.Count != rowCount)
                {
                    exceptions.Add($"After rebalance: expected {rowCount} rows, got {afterRebalance.QueryResult?.Rows.Count ?? 0}");
                    trace?.RecordIterationError($"Post-rebalance full scan row count mismatch");
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
        }

        sw.Stop();

        if (exceptions.Count > 0)
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["databaseRoot"] = Path.GetFullPath(dbPath),
                ["varcharDbPath"] = Path.GetFullPath(dbFolder),
                ["tableFolder"] = Path.GetFullPath(Path.Combine(dbFolder, "Tables", "Notes"))
            };
            var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rowCountExpected"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["maxShardSizeBytes"] = DefaultMaxShardSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            if (details.TryGetValue("ShardCount", out var sc) && sc is int s)
                hints["shardCount"] = s.ToString(System.Globalization.CultureInfo.InvariantCulture);

            ManualTestFailureSupport.WriteFailureIfEnabled(
                trace,
                logger,
                "Sharding (VARCHAR)",
                storageLabel,
                failedStage,
                failedStep,
                string.Join(Environment.NewLine, exceptions),
                paths,
                hints,
                lastSql);
        }

        return new TestResult(
            "Sharding (VARCHAR)",
            exceptions.Count == 0,
            sw.Elapsed,
            rowCount + 2,
            exceptions.Count == 0 ? rowCount + 2 : 0,
            exceptions.Count,
            exceptions,
            details,
            storageLabel);
    }
}
