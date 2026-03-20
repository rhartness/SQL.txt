using System.Diagnostics;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Diagnostics;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// Sharding test: insert many rows with small shard size, then measure query performance.
/// Tests full scan, lookup by Id, lookup by Slug, and group-by (CreatedById).
/// Update these tests when features like JOINs, GROUP BY, or improved index usage are added.
/// </summary>
public static class ShardingTest
{
    private const int PageRowSizeBytes = 480;

    public static async Task<TestResult> RunAsync(
        string dbPath,
        int desiredShards = 5,
        int rowCount = 500,
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
        trace?.SetTestScope("Sharding", storageLabel);

        string? failedStage = null;
        string? failedStep = null;
        string? lastSql = null;
        var wikiDbPath = Path.Combine(dbPath, "WikiDb");

        try
        {
            using (ManualTestTraceScope.Stage(trace, "Setup"))
            {
                failedStage = "Setup";
                if (Directory.Exists(wikiDbPath))
                {
                    using (ManualTestTraceScope.Step(trace, "DeleteOldDb"))
                        Directory.Delete(wikiDbPath, recursive: true);
                }

                var setupSw = Stopwatch.StartNew();
                logger?.Log($"Creating database with Page table (maxShardSize for ~{desiredShards} shards, storage: {storageLabel})...");

                var createDbSql = storageBackend is "binary"
                    ? "CREATE DATABASE WikiDb WITH (storageBackend=binary)"
                    : "CREATE DATABASE WikiDb";
                using (ManualTestTraceScope.Step(trace, "CreateDatabase"))
                {
                    failedStep = "CreateDatabase";
                    lastSql = createDbSql;
                    await engine.ExecuteAsync(createDbSql, dbPath, cancellationToken).ConfigureAwait(false);
                }

                using (ManualTestTraceScope.Step(trace, "CreateUserTable"))
                {
                    failedStep = "CreateUserTable";
                    lastSql = "CREATE TABLE User ...";
                    await engine.ExecuteAsync(
                        "CREATE TABLE User (Id CHAR(10) PRIMARY KEY, Username CHAR(50), Email CHAR(100), CreatedAt CHAR(24))",
                        wikiDbPath, cancellationToken).ConfigureAwait(false);
                }

                var maxShardSize = (rowCount / Math.Max(1, desiredShards)) * PageRowSizeBytes;
                maxShardSize = Math.Max(maxShardSize, 1024);
                var createPageSql =
                    $"CREATE TABLE Page (Id CHAR(10) PRIMARY KEY, Title CHAR(200), Slug CHAR(200), CreatedById CHAR(10), CreatedAt CHAR(24), UpdatedAt CHAR(24), FOREIGN KEY (CreatedById) REFERENCES User(Id)) WITH (maxShardSize={maxShardSize})";

                using (ManualTestTraceScope.Step(trace, "CreatePageTable"))
                {
                    failedStep = "CreatePageTable";
                    lastSql = createPageSql;
                    await engine.ExecuteAsync(createPageSql, wikiDbPath, cancellationToken).ConfigureAwait(false);
                }

                using (ManualTestTraceScope.Step(trace, "CreateIndexes"))
                {
                    failedStep = "CreateIndexes";
                    await engine.ExecuteAsync(
                        "CREATE INDEX IX_Page_Slug ON Page(Slug)",
                        wikiDbPath, cancellationToken).ConfigureAwait(false);
                    await engine.ExecuteAsync(
                        "CREATE INDEX IX_Page_CreatedById ON Page(CreatedById)",
                        wikiDbPath, cancellationToken).ConfigureAwait(false);
                }

                using (ManualTestTraceScope.Step(trace, "SeedUser"))
                {
                    failedStep = "SeedUser";
                    lastSql = "INSERT INTO User ...";
                    await engine.ExecuteAsync(
                        "INSERT INTO User (Id, Username, Email, CreatedAt) VALUES ('1', 'admin', 'admin@wiki.local', '2026-03-17T12:00:00Z')",
                        wikiDbPath, cancellationToken).ConfigureAwait(false);
                }

                setupSw.Stop();
                details["Step_Setup_Ms"] = setupSw.Elapsed.TotalMilliseconds;
            }

            using (ManualTestTraceScope.Stage(trace, "Insert"))
            {
                failedStage = "Insert";
                logger?.Log($"Inserting {rowCount} Page rows (batch INSERT)...");
                var insertSw = Stopwatch.StartNew();
                var values = string.Join(", ", Enumerable.Range(0, rowCount).Select(i =>
                    $"('{i}', 'Page {i}', 'page-{i}', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z')"));
                var insertSql = "INSERT INTO Page (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES " + values;
                using (ManualTestTraceScope.Step(trace, "BatchInsert", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                       {
                           ["rowCount"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                       }))
                {
                    failedStep = "BatchInsert";
                    lastSql = insertSql;
                    await engine.ExecuteAsync(insertSql, wikiDbPath, cancellationToken).ConfigureAwait(false);
                }

                insertSw.Stop();
                var insertMs = insertSw.Elapsed.TotalMilliseconds;
                details["Step_Insert_Ms"] = insertMs;
                details["Step_Insert_Count"] = rowCount;
                details["Avg_Insert_Ms"] = rowCount > 0 ? insertMs / rowCount : 0;

                var tablesPath = Path.Combine(wikiDbPath, "Tables", "Page");
                var shardCount = 0;
                if (Directory.Exists(tablesPath))
                {
                    var pattern = storageBackend is "binary" ? "*.bin" : "*.txt";
                    foreach (var f in Directory.GetFiles(tablesPath, pattern))
                    {
                        var name = Path.GetFileNameWithoutExtension(f);
                        if (name == "Page" || (name.StartsWith("Page_", StringComparison.Ordinal) && name.Length > 5 && int.TryParse(name.AsSpan(5), out _)))
                            shardCount++;
                    }
                }
                details["ShardCount"] = shardCount;
                details["RowCount"] = rowCount;
                trace?.RecordCounter("shardFiles", shardCount, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["table"] = "Page" });

                logger?.Log($"Shard files: {shardCount}");
            }

            using (ManualTestTraceScope.Stage(trace, "Query"))
            {
                failedStage = "Query";
                var q1Sw = Stopwatch.StartNew();
                using (ManualTestTraceScope.Step(trace, "QueryFullScan"))
                {
                    failedStep = "QueryFullScan";
                    lastSql = "SELECT * FROM Page";
                    await engine.ExecuteQueryAsync("SELECT * FROM Page", wikiDbPath, cancellationToken).ConfigureAwait(false);
                }
                q1Sw.Stop();
                details["Step_Query_FullScan_Ms"] = q1Sw.Elapsed.TotalMilliseconds;

                var midId = rowCount / 2;
                var q2Sw = Stopwatch.StartNew();
                var q2 = $"SELECT * FROM Page WHERE Id = '{midId}'";
                using (ManualTestTraceScope.Step(trace, "QueryPkLookup"))
                {
                    failedStep = "QueryPkLookup";
                    lastSql = q2;
                    await engine.ExecuteQueryAsync(q2, wikiDbPath, cancellationToken).ConfigureAwait(false);
                }
                q2Sw.Stop();
                details["Step_Query_ById_Ms"] = q2Sw.Elapsed.TotalMilliseconds;

                var midSlug = rowCount / 2;
                var q3Sw = Stopwatch.StartNew();
                var q3 = $"SELECT * FROM Page WHERE Slug = 'page-{midSlug}'";
                using (ManualTestTraceScope.Step(trace, "QueryBySlugIndex"))
                {
                    failedStep = "QueryBySlugIndex";
                    lastSql = q3;
                    await engine.ExecuteQueryAsync(q3, wikiDbPath, cancellationToken).ConfigureAwait(false);
                }
                q3Sw.Stop();
                details["Step_Query_BySlug_Ms"] = q3Sw.Elapsed.TotalMilliseconds;

                var q4Sw = Stopwatch.StartNew();
                using (ManualTestTraceScope.Step(trace, "QueryByCreatedByIdIndex"))
                {
                    failedStep = "QueryByCreatedByIdIndex";
                    lastSql = "SELECT * FROM Page WHERE CreatedById = '1'";
                    await engine.ExecuteQueryAsync("SELECT * FROM Page WHERE CreatedById = '1'", wikiDbPath, cancellationToken).ConfigureAwait(false);
                }
                q4Sw.Stop();
                details["Step_Query_ByGroup_Ms"] = q4Sw.Elapsed.TotalMilliseconds;
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
        }

        sw.Stop();

        if (exceptions.Count > 0)
        {
            var paths = BuildPaths(dbPath, wikiDbPath);
            var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rowCountExpected"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["desiredShards"] = desiredShards.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            if (details.TryGetValue("ShardCount", out var sc) && sc is int s)
                hints["shardCount"] = s.ToString(System.Globalization.CultureInfo.InvariantCulture);

            ManualTestFailureSupport.WriteFailureIfEnabled(
                trace,
                logger,
                "Sharding",
                storageLabel,
                failedStage,
                failedStep,
                string.Join(Environment.NewLine, exceptions),
                paths,
                hints,
                lastSql);
        }

        return new TestResult(
            "Sharding",
            exceptions.Count == 0,
            sw.Elapsed,
            rowCount + 4,
            exceptions.Count == 0 ? rowCount + 4 : 0,
            exceptions.Count,
            exceptions,
            details,
            storageLabel);
    }

    private static Dictionary<string, string> BuildPaths(string dbPath, string wikiDbPath) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["databaseRoot"] = Path.GetFullPath(dbPath),
            ["wikiDbPath"] = Path.GetFullPath(wikiDbPath),
            ["tableFolder"] = Path.GetFullPath(Path.Combine(wikiDbPath, "Tables", "Page"))
        };
}
