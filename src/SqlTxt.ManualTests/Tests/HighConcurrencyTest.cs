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
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var engine = new DatabaseEngine();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var exceptions = new List<string>();
        var successCount = 0;
        var failureCount = 0;
        var totalOps = 0;
        var lockObj = new object();

        try
        {
            logger?.Log($"Building WikiDb at {dbPath}...");
            await engine.BuildSampleWikiAsync(dbPath, new BuildSampleWikiOptions(Verbose: false, DeleteIfExists: true), cancellationToken).ConfigureAwait(false);

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
                            await engine.ExecuteAsync(
                                $"INSERT INTO User (Id, Username, Email, CreatedAt) VALUES ('{idBase}', 'user{idBase}', 'u{idBase}@test.local', '2026-03-17T12:00:00Z')",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Insert User {idBase}: {ex.Message}"); }
                        }

                        try
                        {
                            await engine.ExecuteAsync(
                                $"INSERT INTO Page (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES ('{idBase}', 'Page {idBase}', 'page-{idBase}', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z')",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Insert Page {idBase}: {ex.Message}"); }
                        }

                        try
                        {
                            await engine.ExecuteAsync(
                                $"INSERT INTO PageContent (Id, PageId, Content, Version, CreatedById, CreatedAt) VALUES ('{idBase}', '{idBase}', 'Content {idBase}', 1, '1', '2026-03-17T12:00:00Z')",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
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
                        try
                        {
                            await engine.ExecuteAsync(
                                $"UPDATE User SET Username = 'updated{idBase}' WHERE Id = '1'",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) { exceptions.Add($"Update User {idBase}: {ex.Message}"); }
                        }

                        try
                        {
                            await engine.ExecuteAsync(
                                $"UPDATE Page SET Title = 'Updated {idBase}' WHERE Id = '1'",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
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
                            await engine.ExecuteAsync(
                                $"DELETE FROM PageContent WHERE Id = '{idBase}'",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
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
                            await engine.ExecuteQueryAsync(
                                "SELECT * FROM Page WITH (NOLOCK)",
                                wikiDbPath, cancellationToken).ConfigureAwait(false);
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

        return new TestResult(
            "High Concurrency",
            passed,
            sw.Elapsed,
            totalOps,
            successCount,
            failureCount,
            exceptions);
    }
}
