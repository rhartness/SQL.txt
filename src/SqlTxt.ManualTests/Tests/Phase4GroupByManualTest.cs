using System.Diagnostics;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Diagnostics;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// Manual test for Phase 4.4 — GROUP BY / aggregates / HAVING. See Phase4_04_GroupBy_Aggregates_Plan.md.
/// </summary>
public static class Phase4GroupByManualTest
{
    public static async Task<TestResult> RunAsync(
        string dbPath,
        string? storageBackend,
        ResultLogger? logger,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var engine = new DatabaseEngine();
        var dbName = "Phase4GroupByDb";
        var wikiPath = Path.Combine(dbPath, dbName);
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        var storageLabel = storageBackend ?? "text";
        trace?.SetTestScope("Phase4 group by", storageLabel);

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
                    "CREATE TABLE G (Id CHAR(5) PRIMARY KEY, GKey CHAR(5), Amount INT)",
                    wikiPath, cancellationToken).ConfigureAwait(false);
                await engine.ExecuteAsync(
                    "INSERT INTO G (Id, GKey, Amount) VALUES ('1', 'A', '10'), ('2', 'A', '20'), ('3', 'B', '5')",
                    wikiPath, cancellationToken).ConfigureAwait(false);
            }

            using (ManualTestTraceScope.Stage(trace, "Query"))
            {
                const string groupSql =
                    "SELECT GKey, COUNT(*) AS Cnt, SUM(Amount) AS S FROM G GROUP BY GKey HAVING COUNT(*) > 1";
                var q = await engine.ExecuteQueryAsync(
                    groupSql,
                    wikiPath,
                    cancellationToken).ConfigureAwait(false);
                var rows = q.QueryResult?.Rows;
                if (rows is null || rows.Count != 1)
                    return Phase4ManualTestHelper.Failed(
                        "Phase4 group by",
                        new InvalidOperationException($"Expected 1 group row, got {rows?.Count ?? 0}"),
                        sw,
                        storageLabel,
                        trace,
                        logger,
                        "Query",
                        "GroupByHaving",
                        wikiPath,
                        groupSql);

                var gk = rows[0].GetValue("GKey");
                var cnt = rows[0].GetValue("Cnt");
                var sum = rows[0].GetValue("S");
                if (!string.Equals(gk, "A", StringComparison.OrdinalIgnoreCase) || cnt != "2" || sum != "30")
                    return Phase4ManualTestHelper.Failed(
                        "Phase4 group by",
                        new InvalidOperationException($"Unexpected aggregate row GKey={gk} Cnt={cnt} S={sum}"),
                        sw,
                        storageLabel,
                        trace,
                        logger,
                        "Query",
                        "ValidateAggregates",
                        wikiPath,
                        groupSql);
            }

            sw.Stop();
            return new TestResult(
                "Phase4 group by",
                true,
                sw.Elapsed,
                1,
                1,
                0,
                Array.Empty<string>(),
                new Dictionary<string, object> { ["GroupsReturned"] = 1 },
                storageLabel);
        }
        catch (Exception ex) when (Phase4ManualTestHelper.ShouldSkipAsNotImplemented(ex))
        {
            sw.Stop();
            logger?.Log($"Phase4 group by: skipped ({ex.GetType().Name}: {ex.Message})");
            return Phase4ManualTestHelper.Skipped(
                "Phase4 group by",
                "Phase 4.4 GROUP BY / aggregates / HAVING not implemented yet.",
                sw,
                storageLabel);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Phase4ManualTestHelper.Failed(
                "Phase4 group by",
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
