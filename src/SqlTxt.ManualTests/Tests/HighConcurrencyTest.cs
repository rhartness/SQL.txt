using System.Collections.Concurrent;
using System.Diagnostics;
using SqlTxt.Contracts;
using SqlTxt.Engine;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// High concurrency test: multiple threads performing INSERT, UPDATE, DELETE concurrently.
/// </summary>
public static class HighConcurrencyTest
{
    public static async Task<TestResult> RunAsync(
        string dbPath,
        int threads = 8,
        int opsPerThread = 50,
        int readerThreads = 0,
        string? storageBackend = null,
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var engine = new DatabaseEngine();
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        var successCount = 0;
        var failureCount = 0;
        var totalOps = 0;
        var lockObj = new object();
        var insertTicks = new ConcurrentBag<long>();
        var updateTicks = new ConcurrentBag<long>();
        var deleteTicks = new ConcurrentBag<long>();
        var selectTicks = new ConcurrentBag<long>();
        var setupMs = 0.0;

        try
        {
            var setupSw = Stopwatch.StartNew();
            logger?.Log($"Building WikiDb at {dbPath} (storage: {storageBackend ?? "text"})...");
            await engine.BuildSampleWikiAsync(dbPath, new BuildSampleWikiOptions(Verbose: false, DeleteIfExists: true, StorageBackend: storageBackend), cancellationToken).ConfigureAwait(false);
            setupSw.Stop();
            setupMs = setupSw.Elapsed.TotalMilliseconds;

            var wikiDbPath = Path.Combine(dbPath, "WikiDb");
            totalOps = (threads * opsPerThread * 3) + (readerThreads * opsPerThread);
            var tasks = new List<Task>();

            for (var t = 0; t < threads; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(async () =>
                {
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        var idBase = 10000 + (threadId * 1000) + i;
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await engine.ExecuteAsync(
                                "INSERT INTO User (Id, Username, Email, CreatedAt) VALUES ('" + idBase + "', 'user" + idBase + "', 'u" + idBase + "@test.local', '2026-03-17T12:00:00Z')",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            insertTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Insert User {idBase}: {ex.Message}"); }
                        }

                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await engine.ExecuteAsync(
                                "INSERT INTO Page (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES ('" + idBase + "', 'Page " + idBase + "', 'page-" + idBase + "', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z')",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            insertTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Insert Page {idBase}: {ex.Message}"); }
                        }

                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await engine.ExecuteAsync(
                                "INSERT INTO PageContent (Id, PageId, Content, Version, CreatedById, CreatedAt) VALUES ('" + idBase + "', '" + idBase + "', 'Content " + idBase + "', 1, '1', '2026-03-17T12:00:00Z')",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            insertTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Insert PageContent {idBase}: {ex.Message}"); }
                        }
                    }
                }, cancellationToken));
            }

            for (var t = 0; t < threads; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(async () =>
                {
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        var idBase = 20000 + (threadId * 1000) + i;
                        var userSql = "UPDATE User SET Username = 'u" + idBase + "' WHERE Id = '1'";
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await engine.ExecuteAsync(userSql, wikiDbPath, cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            updateTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Update User {idBase}: {ex.Message}"); }
                        }

                        var pageSql = "UPDATE Page SET Title = 'T" + idBase + "' WHERE Id = '1'";
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await engine.ExecuteAsync(pageSql, wikiDbPath, cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            updateTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Update Page {idBase}: {ex.Message}"); }
                        }
                    }
                }, cancellationToken));
            }

            for (var t = 0; t < threads; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(async () =>
                {
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        var idBase = 10000 + (threadId * 1000) + i;
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await engine.ExecuteAsync(
                                "DELETE FROM PageContent WHERE Id = '" + idBase + "'",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            deleteTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Delete PageContent {idBase}: {ex.Message}"); }
                        }
                    }
                }, cancellationToken));
            }

            for (var t = 0; t < readerThreads; t++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await engine.ExecuteQueryAsync(
                                "SELECT * FROM Page WITH (NOLOCK)",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            selectTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Select NOLOCK: {ex.Message}"); }
                        }
                    }
                }, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (lockObj) { exceptions.Add($"Setup or teardown: {ex.Message}"); }
        }

        sw.Stop();
        totalOps = successCount + failureCount;
        var passed = failureCount == 0;

        var details = new Dictionary<string, object>();
        details["Step_Setup_Ms"] = setupMs;
        if (insertTicks.Count > 0)
        {
            var avgInsertMs = insertTicks.Average() * 1000.0 / Stopwatch.Frequency;
            details["Step_Insert_Ms"] = insertTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Insert_Count"] = insertTicks.Count;
            details["Avg_Insert_Ms"] = avgInsertMs;
        }
        if (updateTicks.Count > 0)
        {
            var avgUpdateMs = updateTicks.Average() * 1000.0 / Stopwatch.Frequency;
            details["Step_Update_Ms"] = updateTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Update_Count"] = updateTicks.Count;
            details["Avg_Update_Ms"] = avgUpdateMs;
        }
        if (deleteTicks.Count > 0)
        {
            var avgDeleteMs = deleteTicks.Average() * 1000.0 / Stopwatch.Frequency;
            details["Step_Delete_Ms"] = deleteTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Delete_Count"] = deleteTicks.Count;
            details["Avg_Delete_Ms"] = avgDeleteMs;
        }
        if (selectTicks.Count > 0)
        {
            var avgSelectMs = selectTicks.Average() * 1000.0 / Stopwatch.Frequency;
            details["Step_Select_Ms"] = selectTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Select_Count"] = selectTicks.Count;
            details["Avg_Select_Ms"] = avgSelectMs;
        }

        return new TestResult(
            "High Concurrency",
            passed,
            sw.Elapsed,
            totalOps,
            successCount,
            failureCount,
            exceptions,
            details,
            storageBackend ?? "text");
    }
}
