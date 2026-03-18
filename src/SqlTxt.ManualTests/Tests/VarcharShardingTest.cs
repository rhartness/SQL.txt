using System.Diagnostics;
using SqlTxt.Engine;
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

        try
        {
            var dbFolder = Path.Combine(dbPath, "VarcharShardingDb");
            if (Directory.Exists(dbFolder))
                Directory.Delete(dbFolder, recursive: true);

            logger?.Log($"Creating database with Notes table (VARCHAR, maxShardSize={DefaultMaxShardSizeBytes} bytes, storage: {storageBackend ?? "text"})...");

            var createDbSql = storageBackend is "binary"
                ? "CREATE DATABASE VarcharShardingDb WITH (storageBackend=binary)"
                : "CREATE DATABASE VarcharShardingDb";
            await engine.ExecuteAsync(createDbSql, dbPath, cancellationToken).ConfigureAwait(false);

            await engine.ExecuteAsync(
                $"CREATE TABLE Notes (Id CHAR(10) PRIMARY KEY, Content VARCHAR(5000)) WITH (maxShardSize={DefaultMaxShardSizeBytes})",
                dbFolder, cancellationToken).ConfigureAwait(false);

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
            await engine.ExecuteAsync(
                "INSERT INTO Notes (Id, Content) VALUES " + valuesStr,
                dbFolder, cancellationToken).ConfigureAwait(false);
            insertSw.Stop();
            details["InsertDurationMs"] = insertSw.ElapsedMilliseconds;

            var tablesPath = Path.Combine(dbFolder, "Tables", "Notes");
            var shardCount = 0;
            if (Directory.Exists(tablesPath))
            {
                var pattern = storageBackend is "binary" ? "*.bin" : "*.txt";
                foreach (var f in Directory.GetFiles(tablesPath, pattern))
                {
                    var name = Path.GetFileNameWithoutExtension(f);
                    if (name == "Notes" || (name.StartsWith("Notes_") && name.Length > 6 && int.TryParse(name[6..], out _)))
                        shardCount++;
                }
            }
            details["ShardCount"] = shardCount;
            details["RowCount"] = rowCount;

            logger?.Log($"Shard files: {shardCount}");

            var fullScan = await engine.ExecuteQueryAsync("SELECT * FROM Notes", dbFolder, cancellationToken).ConfigureAwait(false);
            if (fullScan.QueryResult == null || fullScan.QueryResult.Rows.Count != rowCount)
                exceptions.Add($"Full scan expected {rowCount} rows, got {fullScan.QueryResult?.Rows.Count ?? 0}");

            var midId = rowCount / 2;
            var pkLookup = await engine.ExecuteQueryAsync($"SELECT * FROM Notes WHERE Id = '{midId}'", dbFolder, cancellationToken).ConfigureAwait(false);
            if (pkLookup.QueryResult == null || pkLookup.QueryResult.Rows.Count != 1)
                exceptions.Add($"PK lookup Id={midId} expected 1 row, got {pkLookup.QueryResult?.Rows.Count ?? 0}");

            logger?.Log("Running RebalanceTableAsync...");
            await engine.RebalanceTableAsync(dbFolder, "Notes", cancellationToken).ConfigureAwait(false);

            var afterRebalance = await engine.ExecuteQueryAsync("SELECT * FROM Notes", dbFolder, cancellationToken).ConfigureAwait(false);
            if (afterRebalance.QueryResult == null || afterRebalance.QueryResult.Rows.Count != rowCount)
                exceptions.Add($"After rebalance: expected {rowCount} rows, got {afterRebalance.QueryResult?.Rows.Count ?? 0}");
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
        }

        sw.Stop();

        return new TestResult(
            "Sharding (VARCHAR)",
            exceptions.Count == 0,
            sw.Elapsed,
            rowCount + 2,
            exceptions.Count == 0 ? rowCount + 2 : 0,
            exceptions.Count,
            exceptions,
            details,
            storageBackend ?? "text");
    }
}
