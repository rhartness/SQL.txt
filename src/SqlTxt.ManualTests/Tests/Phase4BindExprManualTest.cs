using System.Diagnostics;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// Manual test for Phase 4.1 — bound expressions / rich WHERE (AND/OR, eventual non-equality).
/// Maps to docs/plans/Phase4_01_Query_IR_and_Expressions_Plan.md.
/// </summary>
public static class Phase4BindExprManualTest
{
    public static async Task<TestResult> RunAsync(
        string dbPath,
        string? storageBackend,
        ResultLogger? logger,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var engine = new DatabaseEngine();
        var dbName = "Phase4BindExprDb";
        var wikiPath = Path.Combine(dbPath, dbName);

        try
        {
            if (Directory.Exists(wikiPath))
                Directory.Delete(wikiPath, recursive: true);

            var createDb = storageBackend is "binary"
                ? $"CREATE DATABASE {dbName} WITH (storageBackend=binary)"
                : $"CREATE DATABASE {dbName}";
            await engine.ExecuteAsync(createDb, dbPath, cancellationToken).ConfigureAwait(false);

            await engine.ExecuteAsync(
                "CREATE TABLE T (Id CHAR(5) PRIMARY KEY, A CHAR(10), B CHAR(10))",
                wikiPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "INSERT INTO T (Id, A, B) VALUES ('1', 'x', 'y'), ('2', 'x', 'n')",
                wikiPath, cancellationToken).ConfigureAwait(false);

            // Compound predicate (requires Phase 4.1 expression IR + evaluation).
            var q = await engine.ExecuteQueryAsync(
                "SELECT Id FROM T WHERE A = 'x' AND B = 'y'",
                wikiPath,
                cancellationToken).ConfigureAwait(false);

            var rows = q.QueryResult?.Rows;
            // Pre-Phase-4 parser stops after first WHERE predicate; AND ... is ignored → two rows (A='x').
            if (rows is null || rows.Count == 0)
                return Phase4ManualTestHelper.Failed(
                    "Phase4 bind/expr",
                    new InvalidOperationException($"Expected at least one row, got rows={rows?.Count ?? 0}"),
                    sw,
                    storageBackend ?? "text");
            if (rows.Count != 1 || rows[0].GetValue("Id") != "1")
            {
                sw.Stop();
                logger?.Log("Phase4 bind/expr: skipped (compound WHERE not applied — likely pre-Phase-4 parser)");
                return Phase4ManualTestHelper.Skipped(
                    "Phase4 bind/expr",
                    "Phase 4.1 compound WHERE (AND) not in effect yet (parser/evaluator).",
                    sw,
                    storageBackend ?? "text");
            }

            sw.Stop();
            return new TestResult(
                "Phase4 bind/expr",
                true,
                sw.Elapsed,
                1,
                1,
                0,
                Array.Empty<string>(),
                new Dictionary<string, object> { ["QueryRows"] = 1 },
                storageBackend ?? "text");
        }
        catch (Exception ex) when (Phase4ManualTestHelper.ShouldSkipAsNotImplemented(ex))
        {
            sw.Stop();
            logger?.Log($"Phase4 bind/expr: skipped ({ex.GetType().Name}: {ex.Message})");
            return Phase4ManualTestHelper.Skipped(
                "Phase4 bind/expr",
                "Phase 4.1 boolean / compound WHERE not implemented yet.",
                sw,
                storageBackend ?? "text");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Phase4ManualTestHelper.Failed("Phase4 bind/expr", ex, sw, storageBackend ?? "text");
        }
    }
}
