using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlTxt.ManualTests.Diagnostics;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Compare;

/// <summary>
/// Runs manual test equivalents against SQL Server LocalDB for comparison with SQL.txt.
/// Uses default LocalDB instance; no connection config required.
/// </summary>
public static partial class LocalDbComparisons
{
    private const string LocalDbMaster = "Server=(localdb)\\MSSQLLocalDB;Database=master;Integrated Security=True;TrustServerCertificate=True;";

    /// <summary>
    /// Runs concurrency test equivalent against LocalDB.
    /// </summary>
    public static async Task<TestResult> RunConcurrencyAsync(
        int threads = 8,
        int opsPerThread = 50,
        int readerThreads = 0,
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var dbName = $"SqlTxtCompare_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        if (dbName.Length > 128) dbName = dbName[..128]; // SQL Server max DB name length
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
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("High Concurrency", "localdb");
        string? failedStage = null;
        string? failedStep = null;

        try
        {
            var connStr =
                $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
            using (ManualTestTraceScope.Stage(trace, "Setup"))
            {
                failedStage = "Setup";
                var setupSw = Stopwatch.StartNew();
                logger?.Log($"LocalDB comparison: Creating database {dbName}...");

                using (ManualTestTraceScope.Step(trace, "LocalDbCreateAndSchema"))
                {
                    failedStep = "LocalDbCreateAndSchema";
                    await using (var masterConn = new SqlConnection(LocalDbMaster))
                    {
                        await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                        using (var cmd = masterConn.CreateCommand())
                        {
                            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
                            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    await using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

                    // Schema: User, Page, PageContent (SQL Server uses brackets for reserved words)
                    await ExecuteNonQueryAsync(conn, @"
                CREATE TABLE [User] (Id CHAR(10) PRIMARY KEY, Username CHAR(50), Email CHAR(100), CreatedAt CHAR(24));
                CREATE TABLE [Page] (Id CHAR(10) PRIMARY KEY, Title CHAR(200), Slug CHAR(200), CreatedById CHAR(10), CreatedAt CHAR(24), UpdatedAt CHAR(24), FOREIGN KEY (CreatedById) REFERENCES [User](Id));
                CREATE TABLE PageContent (Id CHAR(10) PRIMARY KEY, PageId CHAR(10), Content NVARCHAR(5000), Version INT, CreatedById CHAR(10), CreatedAt CHAR(24), FOREIGN KEY (PageId) REFERENCES [Page](Id), FOREIGN KEY (CreatedById) REFERENCES [User](Id));
                INSERT INTO [User] (Id, Username, Email, CreatedAt) VALUES ('1', 'admin', 'admin@wiki.local', '2026-03-17T12:00:00Z');
                INSERT INTO [Page] (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES ('1', 'Home', 'home', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z');
                INSERT INTO PageContent (Id, PageId, Content, Version, CreatedById, CreatedAt) VALUES ('1', '1', 'Welcome', 1, '1', '2026-03-17T12:00:00Z');
            ", cancellationToken).ConfigureAwait(false);
                }

                setupSw.Stop();
                setupMs = setupSw.Elapsed.TotalMilliseconds;
            }

            totalOps = (threads * opsPerThread * 3) + (readerThreads * opsPerThread);
            var tasks = new List<Task>();

            using (ManualTestTraceScope.Stage(trace, "ConcurrentDml"))
            {
                failedStage = "ConcurrentDml";
                failedStep = "SpawnWorkers";

            for (var t = 0; t < threads; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(async () =>
                {
                    await using var threadConn = new SqlConnection(connStr);
                    await threadConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        var idBase = 10000 + (threadId * 1000) + i;
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await ExecuteNonQueryAsync(threadConn, $"INSERT INTO [User] (Id, Username, Email, CreatedAt) VALUES ('{idBase}', 'user{idBase}', 'u{idBase}@test.local', '2026-03-17T12:00:00Z')", cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            insertTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) exceptions.Add($"Insert User {idBase}: {ex.Message}");
                        }

                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await ExecuteNonQueryAsync(threadConn, $"INSERT INTO [Page] (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES ('{idBase}', 'Page {idBase}', 'page-{idBase}', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z')", cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            insertTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) exceptions.Add($"Insert Page {idBase}: {ex.Message}");
                        }

                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await ExecuteNonQueryAsync(threadConn, $"INSERT INTO PageContent (Id, PageId, Content, Version, CreatedById, CreatedAt) VALUES ('{idBase}', '{idBase}', 'Content {idBase}', 1, '1', '2026-03-17T12:00:00Z')", cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            insertTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) exceptions.Add($"Insert PageContent {idBase}: {ex.Message}");
                        }
                    }
                }, cancellationToken));
            }

            for (var t = 0; t < threads; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(async () =>
                {
                    await using var threadConn = new SqlConnection(connStr);
                    await threadConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        var idBase = 20000 + (threadId * 1000) + i;
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await ExecuteNonQueryAsync(threadConn, $"UPDATE [User] SET Username = 'u{idBase}' WHERE Id = '1'", cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            updateTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) exceptions.Add($"Update User {idBase}: {ex.Message}");
                        }

                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await ExecuteNonQueryAsync(threadConn, $"UPDATE [Page] SET Title = 'T{idBase}' WHERE Id = '1'", cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            updateTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) exceptions.Add($"Update Page {idBase}: {ex.Message}");
                        }
                    }
                }, cancellationToken));
            }

            for (var t = 0; t < threads; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(async () =>
                {
                    await using var threadConn = new SqlConnection(connStr);
                    await threadConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        var idBase = 10000 + (threadId * 1000) + i;
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await ExecuteNonQueryAsync(threadConn, $"DELETE FROM PageContent WHERE Id = '{idBase}'", cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            deleteTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) exceptions.Add($"Delete PageContent {idBase}: {ex.Message}");
                        }
                    }
                }, cancellationToken));
            }

            for (var t = 0; t < readerThreads; t++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await using var threadConn = new SqlConnection(connStr);
                    await threadConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                    for (var i = 0; i < opsPerThread; i++)
                    {
                        try
                        {
                            var opSw = Stopwatch.StartNew();
                            await ExecuteReaderAsync(threadConn, "SELECT * FROM [Page] WITH (NOLOCK)", cancellationToken).ConfigureAwait(false);
                            opSw.Stop();
                            selectTicks.Add(opSw.ElapsedTicks);
                            Interlocked.Increment(ref successCount);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref failureCount);
                            lock (lockObj) exceptions.Add($"Select NOLOCK: {ex.Message}");
                        }
                    }
                }, cancellationToken));
            }

                failedStep = "AwaitWorkers";
                using (ManualTestTraceScope.Step(trace, "AwaitWorkers", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                       {
                           ["writerThreads"] = threads.ToString(System.Globalization.CultureInfo.InvariantCulture),
                           ["readerThreads"] = readerThreads.ToString(System.Globalization.CultureInfo.InvariantCulture)
                       }))
                    await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            using (ManualTestTraceScope.Stage(trace, "Teardown"))
            {
                failedStage = "Teardown";
                failedStep = "DropDatabase";
                // Drop database
                await using (var masterConn = new SqlConnection(LocalDbMaster))
                {
                    await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using (var cmd = masterConn.CreateCommand())
                    {
                        cmd.CommandText = $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]";
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            lock (lockObj) exceptions.Add($"Setup or teardown: {ex.Message}");
            failedStage ??= "Setup";
            failedStep ??= "LocalDbCreateAndSchema";
        }

        sw.Stop();
        totalOps = successCount + failureCount;
        var passed = failureCount == 0;

        var details = new Dictionary<string, object>();
        details["Step_Setup_Ms"] = setupMs;
        if (insertTicks.Count > 0)
        {
            details["Step_Insert_Ms"] = insertTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Insert_Count"] = insertTicks.Count;
            details["Avg_Insert_Ms"] = insertTicks.Average() * 1000.0 / Stopwatch.Frequency;
        }
        if (updateTicks.Count > 0)
        {
            details["Step_Update_Ms"] = updateTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Update_Count"] = updateTicks.Count;
            details["Avg_Update_Ms"] = updateTicks.Average() * 1000.0 / Stopwatch.Frequency;
        }
        if (deleteTicks.Count > 0)
        {
            details["Step_Delete_Ms"] = deleteTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Delete_Count"] = deleteTicks.Count;
            details["Avg_Delete_Ms"] = deleteTicks.Average() * 1000.0 / Stopwatch.Frequency;
        }
        if (selectTicks.Count > 0)
        {
            details["Step_Select_Ms"] = selectTicks.Sum() * 1000.0 / Stopwatch.Frequency;
            details["Step_Select_Count"] = selectTicks.Count;
            details["Avg_Select_Ms"] = selectTicks.Average() * 1000.0 / Stopwatch.Frequency;
        }

        if (!passed)
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["localDbDatabase"] = dbName
            };
            var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["threads"] = threads.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["opsPerThread"] = opsPerThread.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["readerThreads"] = readerThreads.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["failureCount"] = failureCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            ManualTestFailureSupport.WriteFailureIfEnabled(
                trace,
                logger,
                "High Concurrency",
                "localdb",
                failedStage ?? "ConcurrentDml",
                failedStep ?? "WorkerTasks",
                string.Join(Environment.NewLine, exceptions.Take(15)),
                paths,
                hints,
                null);
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
            "localdb");
    }

    /// <summary>
    /// Runs sharding test equivalent against LocalDB (no sharding; measures insert + query performance).
    /// </summary>
    public static async Task<TestResult> RunShardingAsync(
        int desiredShards = 5,
        int rowCount = 500,
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var dbName = $"SqlTxtCompare_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        if (dbName.Length > 128) dbName = dbName[..128]; // SQL Server max DB name length
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        var details = new Dictionary<string, object>();
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("Sharding", "localdb");
        string? failedStage = null;
        string? failedStep = null;

        try
        {
            using (ManualTestTraceScope.Stage(trace, "Setup"))
            {
                failedStage = "Setup";
                var setupSw = Stopwatch.StartNew();
                logger?.Log($"LocalDB comparison: Creating database {dbName} with Page table...");

                using (ManualTestTraceScope.Step(trace, "CreateDatabaseAndSchema"))
                {
                    failedStep = "CreateDatabaseAndSchema";
                    await using (var masterConn = new SqlConnection(LocalDbMaster))
                    {
                        await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                        using (var cmd = masterConn.CreateCommand())
                        {
                            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
                            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    var connStr = $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
                    await using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

                    await ExecuteNonQueryAsync(conn, @"
                CREATE TABLE [User] (Id CHAR(10) PRIMARY KEY, Username CHAR(50), Email CHAR(100), CreatedAt CHAR(24));
                CREATE TABLE [Page] (Id CHAR(10) PRIMARY KEY, Title CHAR(200), Slug CHAR(200), CreatedById CHAR(10), CreatedAt CHAR(24), UpdatedAt CHAR(24), FOREIGN KEY (CreatedById) REFERENCES [User](Id));
                CREATE INDEX IX_Page_Slug ON [Page](Slug);
                CREATE INDEX IX_Page_CreatedById ON [Page](CreatedById);
                INSERT INTO [User] (Id, Username, Email, CreatedAt) VALUES ('1', 'admin', 'admin@wiki.local', '2026-03-17T12:00:00Z');
            ", cancellationToken).ConfigureAwait(false);

                    setupSw.Stop();
                    details["Step_Setup_Ms"] = setupSw.Elapsed.TotalMilliseconds;

                    logger?.Log($"LocalDB: Inserting {rowCount} Page rows (batch INSERT)...");
                    var insertSw = Stopwatch.StartNew();
                    var values = string.Join(", ", Enumerable.Range(0, rowCount).Select(i =>
                        $"('{i}', 'Page {i}', 'page-{i}', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z')"));
                    using (ManualTestTraceScope.Step(trace, "BatchInsert", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                           { ["rowCount"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture) }))
                    {
                        failedStep = "BatchInsert";
                        await ExecuteNonQueryAsync(conn,
                            $"INSERT INTO [Page] (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES {values}",
                            cancellationToken).ConfigureAwait(false);
                    }
                    insertSw.Stop();
                    var insertMs = insertSw.Elapsed.TotalMilliseconds;
                    details["Step_Insert_Ms"] = insertMs;
                    details["Step_Insert_Count"] = rowCount;
                    details["Avg_Insert_Ms"] = rowCount > 0 ? insertMs / rowCount : 0;
                    details["ShardCount"] = 0; // LocalDB has no sharding
                    details["RowCount"] = rowCount;

                    using (ManualTestTraceScope.Stage(trace, "Query"))
                    {
                        failedStage = "Query";
                        var q1Sw = Stopwatch.StartNew();
                        using (ManualTestTraceScope.Step(trace, "QueryFullScan"))
                        {
                            failedStep = "QueryFullScan";
                            await ExecuteReaderAsync(conn, "SELECT * FROM [Page]", cancellationToken).ConfigureAwait(false);
                        }
                        q1Sw.Stop();
                        details["Step_Query_FullScan_Ms"] = q1Sw.Elapsed.TotalMilliseconds;

                        var midId = rowCount / 2;
                        var q2Sw = Stopwatch.StartNew();
                        using (ManualTestTraceScope.Step(trace, "QueryPkLookup"))
                        {
                            failedStep = "QueryPkLookup";
                            await ExecuteReaderAsync(conn, $"SELECT * FROM [Page] WHERE Id = '{midId}'", cancellationToken).ConfigureAwait(false);
                        }
                        q2Sw.Stop();
                        details["Step_Query_ById_Ms"] = q2Sw.Elapsed.TotalMilliseconds;

                        var midSlug = rowCount / 2;
                        var q3Sw = Stopwatch.StartNew();
                        using (ManualTestTraceScope.Step(trace, "QueryBySlugIndex"))
                        {
                            failedStep = "QueryBySlugIndex";
                            await ExecuteReaderAsync(conn, $"SELECT * FROM [Page] WHERE Slug = 'page-{midSlug}'", cancellationToken)
                                .ConfigureAwait(false);
                        }
                        q3Sw.Stop();
                        details["Step_Query_BySlug_Ms"] = q3Sw.Elapsed.TotalMilliseconds;

                        var q4Sw = Stopwatch.StartNew();
                        using (ManualTestTraceScope.Step(trace, "QueryByCreatedByIdIndex"))
                        {
                            failedStep = "QueryByCreatedByIdIndex";
                            await ExecuteReaderAsync(conn, "SELECT * FROM [Page] WHERE CreatedById = '1'", cancellationToken)
                                .ConfigureAwait(false);
                        }
                        q4Sw.Stop();
                        details["Step_Query_ByGroup_Ms"] = q4Sw.Elapsed.TotalMilliseconds;
                    }
                }
            }

            using (ManualTestTraceScope.Stage(trace, "Teardown"))
            {
                failedStage = "Teardown";
                failedStep = "DropDatabase";
                await using (var masterConn = new SqlConnection(LocalDbMaster))
                {
                    await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using (var cmd = masterConn.CreateCommand())
                    {
                        cmd.CommandText =
                            $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]";
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
            failedStage ??= "Setup";
            failedStep ??= "CreateDatabaseAndSchema";
        }

        sw.Stop();

        if (exceptions.Count > 0)
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["localDbDatabase"] = dbName };
            var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rowCount"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["desiredShards"] = desiredShards.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            ManualTestFailureSupport.WriteFailureIfEnabled(
                trace,
                logger,
                "Sharding",
                "localdb",
                failedStage,
                failedStep,
                string.Join(Environment.NewLine, exceptions),
                paths,
                hints,
                null);
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
            "localdb");
    }

    /// <summary>
    /// Runs VARCHAR sharding test equivalent against LocalDB. RebalanceTableAsync is SQL.txt-specific; we run setup, insert, and queries only.
    /// </summary>
    public static async Task<TestResult> RunVarcharShardingAsync(
        int desiredShards = 5,
        int rowCount = 200,
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var dbName = $"SqlTxtCompare_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        if (dbName.Length > 128) dbName = dbName[..128]; // SQL Server max DB name length
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        var details = new Dictionary<string, object>();
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("Sharding (VARCHAR)", "localdb");
        string? failedStage = null;
        string? failedStep = null;

        try
        {
            using (ManualTestTraceScope.Stage(trace, "Setup"))
            {
                failedStage = "Setup";
                var setupSw = Stopwatch.StartNew();
                logger?.Log($"LocalDB comparison: Creating database {dbName} with Notes table (VARCHAR)...");

                using (ManualTestTraceScope.Step(trace, "CreateDatabaseAndTable"))
                {
                    failedStep = "CreateDatabaseAndTable";
                    await using (var masterConn = new SqlConnection(LocalDbMaster))
                    {
                        await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                        using (var cmd = masterConn.CreateCommand())
                        {
                            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
                            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                        }
                    }

                    var connStr =
                        $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
                    await using var conn = new SqlConnection(connStr);
                    await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

                    // NVARCHAR(MAX): avoids SqlClient max 4000 for large Unicode parameters on batch INSERT.
                    // Sql.txt parity target remains VARCHAR(5000); LocalDB uses MAX for client/driver limits only.
                    await ExecuteNonQueryAsync(conn,
                        "CREATE TABLE Notes (Id CHAR(10) PRIMARY KEY, Content NVARCHAR(MAX))", cancellationToken)
                        .ConfigureAwait(false);

                    setupSw.Stop();
                    details["Step_Setup_Ms"] = setupSw.Elapsed.TotalMilliseconds;

                    logger?.Log($"LocalDB: Inserting {rowCount} Notes rows with variable Content lengths...");
                    var random = new Random(42);
                    var values = new List<string>();
                    for (var i = 0; i < rowCount; i++)
                    {
                        var contentLen = 100 + random.Next(1901);
                        var content = new string('x', contentLen).Replace("'", "''");
                        values.Add($"('{i}', '{content}')");
                    }
                    var valuesStr = string.Join(", ", values);
                    var insertSw = Stopwatch.StartNew();
                    using (ManualTestTraceScope.Step(trace, "BatchInsert", new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                           { ["rowCount"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture) }))
                    {
                        failedStep = "BatchInsert";
                        await ExecuteNonQueryAsync(conn, $"INSERT INTO Notes (Id, Content) VALUES {valuesStr}",
                            cancellationToken).ConfigureAwait(false);
                    }
                    insertSw.Stop();
                    var insertMs = insertSw.Elapsed.TotalMilliseconds;
                    details["Step_Insert_Ms"] = insertMs;
                    details["Step_Insert_Count"] = rowCount;
                    details["Avg_Insert_Ms"] = rowCount > 0 ? insertMs / rowCount : 0;
                    details["ShardCount"] = 0;
                    details["RowCount"] = rowCount;

                    using (ManualTestTraceScope.Stage(trace, "AssertQueries"))
                    {
                        failedStage = "AssertQueries";
                        var fullScanSw = Stopwatch.StartNew();
                        using (ManualTestTraceScope.Step(trace, "QueryFullScanPreRebalance"))
                        {
                            failedStep = "QueryFullScanPreRebalance";
                            var fullScanRows = await ExecuteScalarIntAsync(conn, "SELECT COUNT(*) FROM Notes", cancellationToken)
                                .ConfigureAwait(false);
                            fullScanSw.Stop();
                            details["Step_Query_FullScan_Ms"] = fullScanSw.Elapsed.TotalMilliseconds;
                            if (fullScanRows != rowCount)
                                exceptions.Add($"Full scan expected {rowCount} rows, got {fullScanRows}");
                        }

                        var midId = rowCount / 2;
                        var pkLookupSw = Stopwatch.StartNew();
                        using (ManualTestTraceScope.Step(trace, "QueryPkLookupPreRebalance"))
                        {
                            failedStep = "QueryPkLookupPreRebalance";
                            var pkRows = await ExecuteScalarIntAsync(conn, $"SELECT COUNT(*) FROM Notes WHERE Id = '{midId}'",
                                    cancellationToken)
                                .ConfigureAwait(false);
                            pkLookupSw.Stop();
                            details["Step_Query_PkLookup_Ms"] = pkLookupSw.Elapsed.TotalMilliseconds;
                            if (pkRows != 1)
                                exceptions.Add($"PK lookup Id={midId} expected 1 row, got {pkRows}");
                        }
                    }

                    details["Step_Rebalance_Ms"] = 0.0; // N/A for LocalDB
                    details["Step_Query_AfterRebalance_Ms"] = 0.0; // N/A
                }
            }

            using (ManualTestTraceScope.Stage(trace, "Teardown"))
            {
                failedStage = "Teardown";
                failedStep = "DropDatabase";
                await using (var masterConn = new SqlConnection(LocalDbMaster))
                {
                    await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
                    using (var cmd = masterConn.CreateCommand())
                    {
                        cmd.CommandText =
                            $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]";
                        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
            failedStage ??= "Setup";
            failedStep ??= "CreateDatabaseAndTable";
        }

        sw.Stop();

        if (exceptions.Count > 0)
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["localDbDatabase"] = dbName };
            var hints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["rowCount"] = rowCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["desiredShards"] = desiredShards.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };
            ManualTestFailureSupport.WriteFailureIfEnabled(
                trace,
                logger,
                "Sharding (VARCHAR)",
                "localdb",
                failedStage,
                failedStep,
                string.Join(Environment.NewLine, exceptions),
                paths,
                hints,
                null);
        }

        return new TestResult(
            "Sharding (VARCHAR)",
            exceptions.Count == 0,
            sw.Elapsed,
            rowCount + 2,
            exceptions.Count == 0 ? rowCount + 2 : 0,
            exceptions.Count,
            exceptions,
            details,
            "localdb");
    }

    private static async Task ExecuteNonQueryAsync(SqlConnection conn, string sql, CancellationToken ct)
    {
        foreach (var stmt in SplitStatements(sql))
        {
            if (string.IsNullOrWhiteSpace(stmt)) continue;
            using var cmd = conn.CreateCommand();
            cmd.CommandText = stmt;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static async Task ExecuteReaderAsync(SqlConnection conn, string sql, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) { }
    }

    private static async Task<int> ExecuteScalarIntAsync(SqlConnection conn, string sql, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result is int i ? i : Convert.ToInt32(result);
    }

    private static async Task<string?> ExecuteScalarStringAsync(SqlConnection conn, string sql, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result?.ToString();
    }

    private static IEnumerable<string> SplitStatements(string sql)
    {
        var parts = sql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in parts)
        {
            var s = p.Trim();
            if (!string.IsNullOrEmpty(s)) yield return s + ";";
        }
    }
}
