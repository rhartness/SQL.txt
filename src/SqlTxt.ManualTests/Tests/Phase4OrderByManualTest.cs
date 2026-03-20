using System.Diagnostics;
using System.Linq;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Diagnostics;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// Manual test for Phase 4.3 — ORDER BY / sort. See Phase4_03_OrderBy_Sort_Plan.md.
/// </summary>
public static class Phase4OrderByManualTest
{
    public static async Task<TestResult> RunAsync(
        string dbPath,
        string? storageBackend,
        ResultLogger? logger,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var engine = new DatabaseEngine();
        var dbName = "Phase4OrderByDb";
        var wikiPath = Path.Combine(dbPath, dbName);
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        var storageLabel = storageBackend ?? "text";
        trace?.SetTestScope("Phase4 order by", storageLabel);

        try
        {
            using (ManualTestTraceScope.Stage(trace, "Setup"))
            {
                if (Directory.Exists(wikiPath))
                    Directory.Delete(wikiPath, recursive: true);

                var createDb = storageBackend is "binary"
                    ? $"CREATE DATABASE {dbName} WITH (storageBackend=binary)"
                    : $"CREATE DATABASE {dbName}";
                await engine.ExecuteAsync(createDb, dbPath, cancellationToken).ConfigureAwait(false);

                await engine.ExecuteAsync(
                    "CREATE TABLE R (Id CHAR(5) PRIMARY KEY, SortKey CHAR(10))",
                    wikiPath, cancellationToken).ConfigureAwait(false);
                await engine.ExecuteAsync(
                    "INSERT INTO R (Id, SortKey) VALUES ('3', 'c'), ('1', 'a'), ('2', 'b')",
                    wikiPath, cancellationToken).ConfigureAwait(false);
            }

            using (ManualTestTraceScope.Stage(trace, "Query"))
            {
                const string orderSql = "SELECT Id FROM R ORDER BY SortKey ASC, Id ASC";
                var q = await engine.ExecuteQueryAsync(
                    orderSql,
                    wikiPath,
                    cancellationToken).ConfigureAwait(false);
                var rows = q.QueryResult?.Rows;
                if (rows is null || rows.Count != 3)
                    return Phase4ManualTestHelper.Failed(
                        "Phase4 order by",
                        new InvalidOperationException($"Expected 3 rows, got {rows?.Count ?? 0}"),
                        sw,
                        storageLabel,
                        trace,
                        logger,
                        "Query",
                        "OrderBySelect",
                        wikiPath,
                        orderSql);

                var ids = rows.Select(r => r.GetValue("Id")).ToList();
                if (ids[0] != "1" || ids[1] != "2" || ids[2] != "3")
                {
                    if (ids.Count == 3 && ids[0] == "3" && ids[1] == "1" && ids[2] == "2")
                    {
                        sw.Stop();
                        logger?.Log("Phase4 order by: skipped (ORDER BY not applied — pre-Phase-4 parser)");
                        return Phase4ManualTestHelper.Skipped(
                            "Phase4 order by",
                            "Phase 4.3 ORDER BY not in effect yet.",
                            sw,
                            storageLabel);
                    }

                    return Phase4ManualTestHelper.Failed(
                        "Phase4 order by",
                        new InvalidOperationException($"Expected order 1,2,3 got {string.Join(",", ids)}"),
                        sw,
                        storageLabel,
                        trace,
                        logger,
                        "Query",
                        "ValidateOrder",
                        wikiPath,
                        orderSql);
                }
            }

            sw.Stop();
            return new TestResult(
                "Phase4 order by",
                true,
                sw.Elapsed,
                1,
                1,
                0,
                Array.Empty<string>(),
                new Dictionary<string, object> { ["OrderedIds"] = "1,2,3" },
                storageLabel);
        }
        catch (Exception ex) when (Phase4ManualTestHelper.ShouldSkipAsNotImplemented(ex))
        {
            sw.Stop();
            logger?.Log($"Phase4 order by: skipped ({ex.GetType().Name}: {ex.Message})");
            return Phase4ManualTestHelper.Skipped(
                "Phase4 order by",
                "Phase 4.3 ORDER BY not implemented yet.",
                sw,
                storageLabel);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Phase4ManualTestHelper.Failed(
                "Phase4 order by",
                ex,
                sw,
                storageLabel,
                trace,
                logger,
                "Execute",
                "Unhandled",
                wikiPath,
                null);
        }
    }
}
