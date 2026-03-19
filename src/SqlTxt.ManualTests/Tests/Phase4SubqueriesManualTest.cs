using System.Diagnostics;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// Manual test for Phase 4.5 — subqueries (IN, EXISTS, scalar). See Phase4_05_Subqueries_Decorrelation_Plan.md.
/// </summary>
public static class Phase4SubqueriesManualTest
{
    public static async Task<TestResult> RunAsync(
        string dbPath,
        string? storageBackend,
        ResultLogger? logger,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var engine = new DatabaseEngine();
        var dbName = "Phase4SubqueriesDb";
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
                "CREATE TABLE Parent (Id CHAR(5) PRIMARY KEY)",
                wikiPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "CREATE TABLE Child (Id CHAR(5) PRIMARY KEY, PId CHAR(5), FOREIGN KEY (PId) REFERENCES Parent(Id))",
                wikiPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "INSERT INTO Parent (Id) VALUES ('1'), ('2')",
                wikiPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "INSERT INTO Child (Id, PId) VALUES ('c1', '1')",
                wikiPath, cancellationToken).ConfigureAwait(false);

            var qIn = await engine.ExecuteQueryAsync(
                "SELECT Id FROM Parent WHERE Id IN (SELECT PId FROM Child)",
                wikiPath,
                cancellationToken).ConfigureAwait(false);
            var inRows = qIn.QueryResult?.Rows;
            if (inRows is null || inRows.Count != 1 || inRows[0].GetValue("Id") != "1")
                return Phase4ManualTestHelper.Failed(
                    "Phase4 subqueries",
                    new InvalidOperationException("IN (subquery) expected Parent Id 1"),
                    sw,
                    storageBackend ?? "text");

            var qEx = await engine.ExecuteQueryAsync(
                "SELECT Id FROM Parent p WHERE EXISTS (SELECT 1 FROM Child c WHERE c.PId = p.Id)",
                wikiPath,
                cancellationToken).ConfigureAwait(false);
            var exRows = qEx.QueryResult?.Rows;
            if (exRows is null || exRows.Count != 1)
                return Phase4ManualTestHelper.Failed(
                    "Phase4 subqueries",
                    new InvalidOperationException($"EXISTS expected 1 parent row, got {exRows?.Count ?? 0}"),
                    sw,
                    storageBackend ?? "text");

            var qScalar = await engine.ExecuteQueryAsync(
                "SELECT Id, (SELECT COUNT(*) FROM Child c WHERE c.PId = p.Id) AS Cnt FROM Parent p WHERE p.Id = '1'",
                wikiPath,
                cancellationToken).ConfigureAwait(false);
            var scRows = qScalar.QueryResult?.Rows;
            if (scRows is null || scRows.Count != 1 || scRows[0].GetValue("Cnt") != "1")
                return Phase4ManualTestHelper.Failed(
                    "Phase4 subqueries",
                    new InvalidOperationException("Scalar subquery COUNT expected 1"),
                    sw,
                    storageBackend ?? "text");

            sw.Stop();
            return new TestResult(
                "Phase4 subqueries",
                true,
                sw.Elapsed,
                3,
                3,
                0,
                Array.Empty<string>(),
                new Dictionary<string, object> { ["Checks"] = "IN,EXISTS,scalar" },
                storageBackend ?? "text");
        }
        catch (Exception ex) when (Phase4ManualTestHelper.ShouldSkipAsNotImplemented(ex))
        {
            sw.Stop();
            logger?.Log($"Phase4 subqueries: skipped ({ex.GetType().Name}: {ex.Message})");
            return Phase4ManualTestHelper.Skipped(
                "Phase4 subqueries",
                "Phase 4.5 subqueries not implemented yet.",
                sw,
                storageBackend ?? "text");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Phase4ManualTestHelper.Failed("Phase4 subqueries", ex, sw, storageBackend ?? "text");
        }
    }
}
