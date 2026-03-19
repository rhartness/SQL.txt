using System.Diagnostics;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// Manual test for Phase 4.2 — JOIN execution. See Phase4_02_Joins_Execution_Plan.md.
/// </summary>
public static class Phase4JoinsManualTest
{
    public static async Task<TestResult> RunAsync(
        string dbPath,
        string? storageBackend,
        ResultLogger? logger,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var engine = new DatabaseEngine();
        var dbName = "Phase4JoinsDb";
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
                "CREATE TABLE U (Id CHAR(5) PRIMARY KEY, Name CHAR(20))",
                wikiPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "CREATE TABLE O (Id CHAR(5) PRIMARY KEY, UserId CHAR(5), FOREIGN KEY (UserId) REFERENCES U(Id))",
                wikiPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "INSERT INTO U (Id, Name) VALUES ('1', 'Alice')",
                wikiPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "INSERT INTO O (Id, UserId) VALUES ('a', '1'), ('b', '1')",
                wikiPath, cancellationToken).ConfigureAwait(false);

            var inner = await engine.ExecuteQueryAsync(
                "SELECT u.Id, u.Name, o.Id AS OrderId FROM U u INNER JOIN O o ON u.Id = o.UserId",
                wikiPath,
                cancellationToken).ConfigureAwait(false);
            var innerRows = inner.QueryResult?.Rows;
            if (innerRows is null || innerRows.Count != 2)
                return Phase4ManualTestHelper.Failed(
                    "Phase4 joins",
                    new InvalidOperationException($"INNER JOIN expected 2 rows, got {innerRows?.Count ?? 0}"),
                    sw,
                    storageBackend ?? "text");

            var left = await engine.ExecuteQueryAsync(
                "SELECT u.Id, o.Id AS OrderId FROM U u LEFT JOIN O o ON u.Id = o.UserId",
                wikiPath,
                cancellationToken).ConfigureAwait(false);
            var leftRows = left.QueryResult?.Rows;
            if (leftRows is null || leftRows.Count < 2)
                return Phase4ManualTestHelper.Failed(
                    "Phase4 joins",
                    new InvalidOperationException($"LEFT JOIN expected at least 2 rows, got {leftRows?.Count ?? 0}"),
                    sw,
                    storageBackend ?? "text");

            sw.Stop();
            return new TestResult(
                "Phase4 joins",
                true,
                sw.Elapsed,
                2,
                2,
                0,
                Array.Empty<string>(),
                new Dictionary<string, object> { ["InnerRows"] = innerRows.Count, ["LeftRows"] = leftRows.Count },
                storageBackend ?? "text");
        }
        catch (Exception ex) when (Phase4ManualTestHelper.ShouldSkipAsNotImplemented(ex))
        {
            sw.Stop();
            logger?.Log($"Phase4 joins: skipped ({ex.GetType().Name}: {ex.Message})");
            return Phase4ManualTestHelper.Skipped(
                "Phase4 joins",
                "Phase 4.2 JOIN syntax/execution not implemented yet.",
                sw,
                storageBackend ?? "text");
        }
        catch (Exception ex)
        {
            sw.Stop();
            return Phase4ManualTestHelper.Failed("Phase4 joins", ex, sw, storageBackend ?? "text");
        }
    }
}
