using System.Diagnostics;
using SqlTxt.Engine;
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

        try
        {
            var wikiDbPath = Path.Combine(dbPath, "WikiDb");
            if (Directory.Exists(wikiDbPath))
                Directory.Delete(wikiDbPath, recursive: true);

            logger?.Log($"Creating database with Page table (maxShardSize for ~{desiredShards} shards, storage: {storageBackend ?? "text"})...");

            var createDbSql = storageBackend is "binary"
                ? "CREATE DATABASE WikiDb WITH (storageBackend=binary)"
                : "CREATE DATABASE WikiDb";
            await engine.ExecuteAsync(createDbSql, dbPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "CREATE TABLE User (Id CHAR(10) PRIMARY KEY, Username CHAR(50), Email CHAR(100), CreatedAt CHAR(24))",
                wikiDbPath, cancellationToken).ConfigureAwait(false);

            var maxShardSize = (rowCount / Math.Max(1, desiredShards)) * PageRowSizeBytes;
            maxShardSize = Math.Max(maxShardSize, 1024);

            await engine.ExecuteAsync(
                $"CREATE TABLE Page (Id CHAR(10) PRIMARY KEY, Title CHAR(200), Slug CHAR(200), CreatedById CHAR(10), CreatedAt CHAR(24), UpdatedAt CHAR(24), FOREIGN KEY (CreatedById) REFERENCES User(Id)) WITH (maxShardSize={maxShardSize})",
                wikiDbPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "CREATE INDEX IX_Page_Slug ON Page(Slug)",
                wikiDbPath, cancellationToken).ConfigureAwait(false);
            await engine.ExecuteAsync(
                "CREATE INDEX IX_Page_CreatedById ON Page(CreatedById)",
                wikiDbPath, cancellationToken).ConfigureAwait(false);

            await engine.ExecuteAsync(
                "INSERT INTO User (Id, Username, Email, CreatedAt) VALUES ('1', 'admin', 'admin@wiki.local', '2026-03-17T12:00:00Z')",
                wikiDbPath, cancellationToken).ConfigureAwait(false);

            logger?.Log($"Inserting {rowCount} Page rows (batch INSERT)...");
            var insertSw = Stopwatch.StartNew();
            var values = string.Join(", ", Enumerable.Range(0, rowCount).Select(i =>
                $"('{i}', 'Page {i}', 'page-{i}', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z')"));
            await engine.ExecuteAsync(
                "INSERT INTO Page (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES " + values,
                wikiDbPath, cancellationToken).ConfigureAwait(false);
            insertSw.Stop();
            details["InsertDurationMs"] = insertSw.ElapsedMilliseconds;

            var tablesPath = Path.Combine(wikiDbPath, "Tables", "Page");
            var shardCount = 0;
            if (Directory.Exists(tablesPath))
            {
                var pattern = storageBackend is "binary" ? "*.bin" : "*.txt";
                foreach (var f in Directory.GetFiles(tablesPath, pattern))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (name == "Page" || (name.StartsWith("Page_") && name.Length > 5 && int.TryParse(name[5..], out _)))
                        shardCount++;
                }
            }
            details["ShardCount"] = shardCount;
            details["RowCount"] = rowCount;

            logger?.Log($"Shard files: {shardCount}");

            var queryTimings = new List<long>();

            // Full table scan
            var q1Sw = Stopwatch.StartNew();
            await engine.ExecuteQueryAsync("SELECT * FROM Page", wikiDbPath, cancellationToken).ConfigureAwait(false);
            q1Sw.Stop();
            queryTimings.Add(q1Sw.ElapsedMilliseconds);
            details["QueryFullScanMs"] = q1Sw.ElapsedMilliseconds;

            // Lookup by primary key (Id)
            var midId = rowCount / 2;
            var q2Sw = Stopwatch.StartNew();
            await engine.ExecuteQueryAsync($"SELECT * FROM Page WHERE Id = '{midId}'", wikiDbPath, cancellationToken).ConfigureAwait(false);
            q2Sw.Stop();
            queryTimings.Add(q2Sw.ElapsedMilliseconds);
            details["QueryByIdMs"] = q2Sw.ElapsedMilliseconds;

            // Lookup by indexed column (Slug)
            var midSlug = rowCount / 2;
            var q3Sw = Stopwatch.StartNew();
            await engine.ExecuteQueryAsync($"SELECT * FROM Page WHERE Slug = 'page-{midSlug}'", wikiDbPath, cancellationToken).ConfigureAwait(false);
            q3Sw.Stop();
            queryTimings.Add(q3Sw.ElapsedMilliseconds);
            details["QueryBySlugMs"] = q3Sw.ElapsedMilliseconds;

            // Group-by style: all rows for a given CreatedById (indexed FK)
            var q4Sw = Stopwatch.StartNew();
            await engine.ExecuteQueryAsync("SELECT * FROM Page WHERE CreatedById = '1'", wikiDbPath, cancellationToken).ConfigureAwait(false);
            q4Sw.Stop();
            queryTimings.Add(q4Sw.ElapsedMilliseconds);
            details["QueryByGroupMs"] = q4Sw.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
        }

        sw.Stop();

        return new TestResult(
            "Sharding",
            exceptions.Count == 0,
            sw.Elapsed,
            rowCount + 4,
            exceptions.Count == 0 ? rowCount + 4 : 0,
            exceptions.Count,
            exceptions,
            details,
            storageBackend ?? "text");
    }
}
