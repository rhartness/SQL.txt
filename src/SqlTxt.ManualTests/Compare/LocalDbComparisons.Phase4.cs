using System.Diagnostics;
using Microsoft.Data.SqlClient;
using SqlTxt.ManualTests.Diagnostics;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Compare;

/// <summary>
/// LocalDB equivalents for Phase 4 manual tests (parity with SqlTxt.Engine scenarios).
/// </summary>
public static partial class LocalDbComparisons
{
    private static async Task<(string DbName, string ConnStr)> CreateTempCompareDbAsync(CancellationToken cancellationToken)
    {
        var dbName = $"SqlTxtCompare_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}";
        if (dbName.Length > 128) dbName = dbName[..128];
        await using (var masterConn = new SqlConnection(LocalDbMaster))
        {
            await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
            using var cmd = masterConn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{dbName}]";
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        var connStr =
            $"Server=(localdb)\\MSSQLLocalDB;Database={dbName};Integrated Security=True;TrustServerCertificate=True;";
        return (dbName, connStr);
    }

    private static async Task DropTempCompareDbAsync(string dbName, CancellationToken cancellationToken)
    {
        await using var masterConn = new SqlConnection(LocalDbMaster);
        await masterConn.OpenAsync(cancellationToken).ConfigureAwait(false);
        using var cmd = masterConn.CreateCommand();
        cmd.CommandText = $"ALTER DATABASE [{dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{dbName}]";
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void WriteLocalDbPhase4FailureBundle(
        ManualTestTrace? trace,
        ResultLogger? logger,
        string testName,
        string? dbName,
        string? failedStage,
        string? failedStep,
        IReadOnlyList<string> exceptions)
    {
        if (exceptions.Count == 0)
            return;
        IReadOnlyDictionary<string, string>? paths = dbName is null
            ? null
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["localDbDatabase"] = dbName };
        ManualTestFailureSupport.WriteFailureIfEnabled(
            trace,
            logger,
            testName,
            "localdb",
            failedStage ?? "LocalDbPhase4",
            failedStep ?? "Execute",
            string.Join(Environment.NewLine, exceptions),
            paths,
            null,
            null);
    }

    /// <summary>Matches <see cref="Tests.Phase4BindExprManualTest"/>.</summary>
    public static async Task<TestResult> RunPhase4BindExprAsync(
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        string? dbName = null;
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("Phase4 bind/expr", "localdb");
        string? failedStage = null;
        string? failedStep = null;
        try
        {
            logger?.Log("LocalDB Phase4: bind-expr (compound WHERE)");
            using (ManualTestTraceScope.Stage(trace, "LocalDbPhase4"))
            {
                failedStage = "LocalDbPhase4";
                using (ManualTestTraceScope.Step(trace, "SetupAndQuery"))
                {
                    failedStep = "SetupAndQuery";
            (dbName, var connStr) = await CreateTempCompareDbAsync(cancellationToken).ConfigureAwait(false);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                conn,
                @"CREATE TABLE T (Id CHAR(5) PRIMARY KEY, A CHAR(10), B CHAR(10));
                  INSERT INTO T (Id, A, B) VALUES ('1', 'x', 'y'), ('2', 'x', 'n');",
                cancellationToken).ConfigureAwait(false);

            var cnt = await ExecuteScalarIntAsync(
                conn,
                "SELECT COUNT(*) FROM T WHERE RTRIM(A) = 'x' AND RTRIM(B) = 'y'",
                cancellationToken).ConfigureAwait(false);
            if (cnt != 1)
                exceptions.Add($"bind-expr: expected 1 row, got {cnt}");
            var id = await ExecuteScalarStringAsync(
                    conn,
                    "SELECT RTRIM(Id) FROM T WHERE RTRIM(A) = 'x' AND RTRIM(B) = 'y'",
                    cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(id?.Trim(), "1", StringComparison.OrdinalIgnoreCase))
                exceptions.Add($"bind-expr: expected Id 1, got {id}");
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
            failedStage ??= "LocalDbPhase4";
            failedStep ??= "SetupAndQuery";
        }
        finally
        {
            if (dbName is not null)
            {
                try
                {
                    await DropTempCompareDbAsync(dbName, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    /* best-effort */
                }
            }
        }

        sw.Stop();
        WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 bind/expr", dbName, failedStage, failedStep, exceptions);
        return new TestResult(
            "Phase4 bind/expr",
            exceptions.Count == 0,
            sw.Elapsed,
            1,
            exceptions.Count == 0 ? 1 : 0,
            exceptions.Count,
            exceptions,
            null,
            "localdb");
    }

    /// <summary>Matches <see cref="Tests.Phase4JoinsManualTest"/>.</summary>
    public static async Task<TestResult> RunPhase4JoinsAsync(
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        string? dbName = null;
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("Phase4 joins", "localdb");
        string? failedStage = null;
        string? failedStep = null;
        try
        {
            logger?.Log("LocalDB Phase4: joins");
            using (ManualTestTraceScope.Stage(trace, "LocalDbPhase4"))
            {
                failedStage = "LocalDbPhase4";
                using (ManualTestTraceScope.Step(trace, "SetupAndQuery"))
                {
                    failedStep = "SetupAndQuery";
            (dbName, var connStr) = await CreateTempCompareDbAsync(cancellationToken).ConfigureAwait(false);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                conn,
                @"CREATE TABLE U (Id CHAR(5) PRIMARY KEY, Name CHAR(20));
                  CREATE TABLE O (Id CHAR(5) PRIMARY KEY, UserId CHAR(5), FOREIGN KEY (UserId) REFERENCES U(Id));
                  INSERT INTO U (Id, Name) VALUES ('1', 'Alice');
                  INSERT INTO O (Id, UserId) VALUES ('a', '1'), ('b', '1');",
                cancellationToken).ConfigureAwait(false);

            var innerCnt = await ExecuteScalarIntAsync(
                conn,
                "SELECT COUNT(*) FROM U u INNER JOIN O o ON RTRIM(u.Id) = RTRIM(o.UserId)",
                cancellationToken).ConfigureAwait(false);
            if (innerCnt != 2)
                exceptions.Add($"joins INNER: expected 2 rows, got {innerCnt}");

            var leftCnt = await ExecuteScalarIntAsync(
                conn,
                "SELECT COUNT(*) FROM U u LEFT JOIN O o ON RTRIM(u.Id) = RTRIM(o.UserId)",
                cancellationToken).ConfigureAwait(false);
            if (leftCnt < 2)
                exceptions.Add($"joins LEFT: expected at least 2 rows, got {leftCnt}");
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
            failedStage ??= "LocalDbPhase4";
            failedStep ??= "SetupAndQuery";
        }
        finally
        {
            if (dbName is not null)
            {
                try
                {
                    await DropTempCompareDbAsync(dbName, cancellationToken).ConfigureAwait(false);
                }
                catch { }
            }
        }

        sw.Stop();
        WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 joins", dbName, failedStage, failedStep, exceptions);
        return new TestResult(
            "Phase4 joins",
            exceptions.Count == 0,
            sw.Elapsed,
            2,
            exceptions.Count == 0 ? 2 : 0,
            exceptions.Count,
            exceptions,
            null,
            "localdb");
    }

    /// <summary>Matches <see cref="Tests.Phase4OrderByManualTest"/>.</summary>
    public static async Task<TestResult> RunPhase4OrderByAsync(
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        string? dbName = null;
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("Phase4 order by", "localdb");
        string? failedStage = null;
        string? failedStep = null;
        try
        {
            logger?.Log("LocalDB Phase4: order by");
            using (ManualTestTraceScope.Stage(trace, "LocalDbPhase4"))
            {
                failedStage = "LocalDbPhase4";
                using (ManualTestTraceScope.Step(trace, "SetupAndQuery"))
                {
                    failedStep = "SetupAndQuery";
            (dbName, var connStr) = await CreateTempCompareDbAsync(cancellationToken).ConfigureAwait(false);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                conn,
                @"CREATE TABLE R (Id CHAR(5) PRIMARY KEY, SortKey CHAR(10));
                  INSERT INTO R (Id, SortKey) VALUES ('3', 'c'), ('1', 'a'), ('2', 'b');",
                cancellationToken).ConfigureAwait(false);

            var ordered = new List<string>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT RTRIM(Id) FROM R ORDER BY RTRIM(SortKey) ASC, RTRIM(Id) ASC";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                    ordered.Add(r.GetString(0).Trim());
            }

            if (ordered.Count != 3)
                exceptions.Add($"orderby: expected 3 rows, got {ordered.Count}");
            else if (ordered[0] != "1" || ordered[1] != "2" || ordered[2] != "3")
                exceptions.Add($"orderby: expected 1,2,3 got {string.Join(',', ordered)}");
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
            failedStage ??= "LocalDbPhase4";
            failedStep ??= "SetupAndQuery";
        }
        finally
        {
            if (dbName is not null)
            {
                try
                {
                    await DropTempCompareDbAsync(dbName, cancellationToken).ConfigureAwait(false);
                }
                catch { }
            }
        }

        sw.Stop();
        WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 order by", dbName, failedStage, failedStep, exceptions);
        return new TestResult(
            "Phase4 order by",
            exceptions.Count == 0,
            sw.Elapsed,
            1,
            exceptions.Count == 0 ? 1 : 0,
            exceptions.Count,
            exceptions,
            null,
            "localdb");
    }

    /// <summary>Matches <see cref="Tests.Phase4GroupByManualTest"/>.</summary>
    public static async Task<TestResult> RunPhase4GroupByAsync(
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        string? dbName = null;
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("Phase4 group by", "localdb");
        string? failedStage = null;
        string? failedStep = null;
        try
        {
            logger?.Log("LocalDB Phase4: group by");
            using (ManualTestTraceScope.Stage(trace, "LocalDbPhase4"))
            {
                failedStage = "LocalDbPhase4";
                using (ManualTestTraceScope.Step(trace, "SetupAndQuery"))
                {
                    failedStep = "SetupAndQuery";
            (dbName, var connStr) = await CreateTempCompareDbAsync(cancellationToken).ConfigureAwait(false);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                conn,
                @"CREATE TABLE G (Id CHAR(5) PRIMARY KEY, GKey CHAR(5), Amount INT);
                  INSERT INTO G (Id, GKey, Amount) VALUES ('1', 'A', 10), ('2', 'A', 20), ('3', 'B', 5);",
                cancellationToken).ConfigureAwait(false);

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT RTRIM(GKey) AS GKey, COUNT(*) AS Cnt, SUM(Amount) AS S FROM G GROUP BY GKey HAVING COUNT(*) > 1";
                await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    exceptions.Add("groupby: expected 1 group row");
                }
                else
                {
                    var gk = r.GetString(0).Trim();
                    var cnt = Convert.ToInt32(r.GetValue(1));
                    var sum = Convert.ToInt32(r.GetValue(2));
                    if (!string.Equals(gk, "A", StringComparison.OrdinalIgnoreCase) || cnt != 2 || sum != 30)
                        exceptions.Add($"groupby: unexpected row GKey={gk} Cnt={cnt} S={sum}");
                    if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                        exceptions.Add("groupby: expected exactly 1 row");
                }
            }
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
            failedStage ??= "LocalDbPhase4";
            failedStep ??= "SetupAndQuery";
        }
        finally
        {
            if (dbName is not null)
            {
                try
                {
                    await DropTempCompareDbAsync(dbName, cancellationToken).ConfigureAwait(false);
                }
                catch { }
            }
        }

        sw.Stop();
        WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 group by", dbName, failedStage, failedStep, exceptions);
        return new TestResult(
            "Phase4 group by",
            exceptions.Count == 0,
            sw.Elapsed,
            1,
            exceptions.Count == 0 ? 1 : 0,
            exceptions.Count,
            exceptions,
            null,
            "localdb");
    }

    /// <summary>Matches <see cref="Tests.Phase4SubqueriesManualTest"/>.</summary>
    public static async Task<TestResult> RunPhase4SubqueriesAsync(
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var exceptions = new List<string>();
        string? dbName = null;
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        trace?.SetTestScope("Phase4 subqueries", "localdb");
        string? failedStage = null;
        string? failedStep = null;
        try
        {
            logger?.Log("LocalDB Phase4: subqueries");
            using (ManualTestTraceScope.Stage(trace, "LocalDbPhase4"))
            {
                failedStage = "LocalDbPhase4";
                using (ManualTestTraceScope.Step(trace, "SetupAndQuery"))
                {
                    failedStep = "SetupAndQuery";
            (dbName, var connStr) = await CreateTempCompareDbAsync(cancellationToken).ConfigureAwait(false);
            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);
            await ExecuteNonQueryAsync(
                conn,
                @"CREATE TABLE Parent (Id CHAR(5) PRIMARY KEY);
                  CREATE TABLE Child (Id CHAR(5) PRIMARY KEY, PId CHAR(5), FOREIGN KEY (PId) REFERENCES Parent(Id));
                  INSERT INTO Parent (Id) VALUES ('1'), ('2');
                  INSERT INTO Child (Id, PId) VALUES ('c1', '1');",
                cancellationToken).ConfigureAwait(false);

            var inCnt = await ExecuteScalarIntAsync(
                conn,
                "SELECT COUNT(*) FROM Parent WHERE RTRIM(Id) IN (SELECT RTRIM(PId) FROM Child)",
                cancellationToken).ConfigureAwait(false);
            if (inCnt != 1)
                exceptions.Add($"subquery IN: expected 1 row, got {inCnt}");
            var inId = await ExecuteScalarStringAsync(
                conn,
                "SELECT RTRIM(Id) FROM Parent WHERE RTRIM(Id) IN (SELECT RTRIM(PId) FROM Child)",
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(inId?.Trim(), "1", StringComparison.OrdinalIgnoreCase))
                exceptions.Add($"subquery IN: expected Id 1, got {inId}");

            var exCnt = await ExecuteScalarIntAsync(
                conn,
                "SELECT COUNT(*) FROM Parent p WHERE EXISTS (SELECT 1 FROM Child c WHERE RTRIM(c.PId) = RTRIM(p.Id))",
                cancellationToken).ConfigureAwait(false);
            if (exCnt != 1)
                exceptions.Add($"subquery EXISTS: expected 1 row, got {exCnt}");

            var scalarCnt = await ExecuteScalarStringAsync(
                conn,
                @"SELECT CAST((SELECT COUNT(*) FROM Child c WHERE RTRIM(c.PId) = RTRIM(p.Id)) AS VARCHAR(20))
                  FROM Parent p WHERE RTRIM(p.Id) = '1'",
                cancellationToken).ConfigureAwait(false);
            if (!string.Equals(scalarCnt?.Trim(), "1", StringComparison.OrdinalIgnoreCase))
                exceptions.Add($"scalar COUNT: expected 1, got {scalarCnt}");
                }
            }
        }
        catch (Exception ex)
        {
            exceptions.Add(ex.ToString());
            failedStage ??= "LocalDbPhase4";
            failedStep ??= "SetupAndQuery";
        }
        finally
        {
            if (dbName is not null)
            {
                try
                {
                    await DropTempCompareDbAsync(dbName, cancellationToken).ConfigureAwait(false);
                }
                catch { }
            }
        }

        sw.Stop();
        WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 subqueries", dbName, failedStage, failedStep, exceptions);
        return new TestResult(
            "Phase4 subqueries",
            exceptions.Count == 0,
            sw.Elapsed,
            3,
            exceptions.Count == 0 ? 3 : 0,
            exceptions.Count,
            exceptions,
            null,
            "localdb");
    }

    /// <summary>
    /// Runs all five Phase 4 LocalDB checks in one database to amortize CREATE/DROP cost (suite driver only).
    /// Each <see cref="TestResult.Duration"/> covers that scenario's DDL and queries only, not database provisioning.
    /// </summary>
    public static async Task<IReadOnlyList<TestResult>> RunPhase4SuiteSingleDatabaseAsync(
        ResultLogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestResult>();
        string? dbName = null;
        var trace = ManualTestRunContext.CurrentOrFallback?.Trace;
        double provisionMs = 0;

        TestResult Finish(string testName, bool passed, Stopwatch sw, int ops, int ok, int fail, List<string> ex)
        {
            var details = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Phase4LocalDbSingleDatabase"] = true,
                ["LocalDbProvisionMs"] = provisionMs
            };
            return new TestResult(testName, passed, sw.Elapsed, ops, ok, fail, ex, details, "localdb");
        }

        try
        {
            var p = Stopwatch.StartNew();
            (dbName, var connStr) = await CreateTempCompareDbAsync(cancellationToken).ConfigureAwait(false);
            p.Stop();
            provisionMs = p.Elapsed.TotalMilliseconds;

            await using var conn = new SqlConnection(connStr);
            await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

            // --- bind-expr ---
            {
                trace?.SetTestScope("Phase4 bind/expr", "localdb");
                var sw = Stopwatch.StartNew();
                var exceptions = new List<string>();
                try
                {
                    logger?.Log("LocalDB Phase4 (single DB): bind-expr");
                    await ExecuteNonQueryAsync(
                        conn,
                        @"CREATE TABLE T (Id CHAR(5) PRIMARY KEY, A CHAR(10), B CHAR(10));
                          INSERT INTO T (Id, A, B) VALUES ('1', 'x', 'y'), ('2', 'x', 'n');",
                        cancellationToken).ConfigureAwait(false);

                    var cnt = await ExecuteScalarIntAsync(
                        conn,
                        "SELECT COUNT(*) FROM T WHERE RTRIM(A) = 'x' AND RTRIM(B) = 'y'",
                        cancellationToken).ConfigureAwait(false);
                    if (cnt != 1)
                        exceptions.Add($"bind-expr: expected 1 row, got {cnt}");
                    var id = await ExecuteScalarStringAsync(
                            conn,
                            "SELECT RTRIM(Id) FROM T WHERE RTRIM(A) = 'x' AND RTRIM(B) = 'y'",
                            cancellationToken)
                        .ConfigureAwait(false);
                    if (!string.Equals(id?.Trim(), "1", StringComparison.OrdinalIgnoreCase))
                        exceptions.Add($"bind-expr: expected Id 1, got {id}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex.ToString());
                }

                sw.Stop();
                WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 bind/expr", dbName, "LocalDbPhase4", "SetupAndQuery", exceptions);
                results.Add(Finish("Phase4 bind/expr", exceptions.Count == 0, sw, 1, exceptions.Count == 0 ? 1 : 0, exceptions.Count, exceptions));
            }

            // --- joins ---
            {
                trace?.SetTestScope("Phase4 joins", "localdb");
                var sw = Stopwatch.StartNew();
                var exceptions = new List<string>();
                try
                {
                    logger?.Log("LocalDB Phase4 (single DB): joins");
                    await ExecuteNonQueryAsync(
                        conn,
                        @"CREATE TABLE U (Id CHAR(5) PRIMARY KEY, Name CHAR(20));
                          CREATE TABLE O (Id CHAR(5) PRIMARY KEY, UserId CHAR(5), FOREIGN KEY (UserId) REFERENCES U(Id));
                          INSERT INTO U (Id, Name) VALUES ('1', 'Alice');
                          INSERT INTO O (Id, UserId) VALUES ('a', '1'), ('b', '1');",
                        cancellationToken).ConfigureAwait(false);

                    var innerCnt = await ExecuteScalarIntAsync(
                        conn,
                        "SELECT COUNT(*) FROM U u INNER JOIN O o ON RTRIM(u.Id) = RTRIM(o.UserId)",
                        cancellationToken).ConfigureAwait(false);
                    if (innerCnt != 2)
                        exceptions.Add($"joins INNER: expected 2 rows, got {innerCnt}");

                    var leftCnt = await ExecuteScalarIntAsync(
                        conn,
                        "SELECT COUNT(*) FROM U u LEFT JOIN O o ON RTRIM(u.Id) = RTRIM(o.UserId)",
                        cancellationToken).ConfigureAwait(false);
                    if (leftCnt < 2)
                        exceptions.Add($"joins LEFT: expected at least 2 rows, got {leftCnt}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex.ToString());
                }

                sw.Stop();
                WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 joins", dbName, "LocalDbPhase4", "SetupAndQuery", exceptions);
                results.Add(Finish("Phase4 joins", exceptions.Count == 0, sw, 2, exceptions.Count == 0 ? 2 : 0, exceptions.Count, exceptions));
            }

            // --- order by ---
            {
                trace?.SetTestScope("Phase4 order by", "localdb");
                var sw = Stopwatch.StartNew();
                var exceptions = new List<string>();
                try
                {
                    logger?.Log("LocalDB Phase4 (single DB): order by");
                    await ExecuteNonQueryAsync(
                        conn,
                        @"CREATE TABLE R (Id CHAR(5) PRIMARY KEY, SortKey CHAR(10));
                          INSERT INTO R (Id, SortKey) VALUES ('3', 'c'), ('1', 'a'), ('2', 'b');",
                        cancellationToken).ConfigureAwait(false);

                    var ordered = new List<string>();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT RTRIM(Id) FROM R ORDER BY RTRIM(SortKey) ASC, RTRIM(Id) ASC";
                        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                            ordered.Add(r.GetString(0).Trim());
                    }

                    if (ordered.Count != 3)
                        exceptions.Add($"orderby: expected 3 rows, got {ordered.Count}");
                    else if (ordered[0] != "1" || ordered[1] != "2" || ordered[2] != "3")
                        exceptions.Add($"orderby: expected 1,2,3 got {string.Join(',', ordered)}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex.ToString());
                }

                sw.Stop();
                WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 order by", dbName, "LocalDbPhase4", "SetupAndQuery", exceptions);
                results.Add(Finish("Phase4 order by", exceptions.Count == 0, sw, 1, exceptions.Count == 0 ? 1 : 0, exceptions.Count, exceptions));
            }

            // --- group by ---
            {
                trace?.SetTestScope("Phase4 group by", "localdb");
                var sw = Stopwatch.StartNew();
                var exceptions = new List<string>();
                try
                {
                    logger?.Log("LocalDB Phase4 (single DB): group by");
                    await ExecuteNonQueryAsync(
                        conn,
                        @"CREATE TABLE G (Id CHAR(5) PRIMARY KEY, GKey CHAR(5), Amount INT);
                          INSERT INTO G (Id, GKey, Amount) VALUES ('1', 'A', 10), ('2', 'A', 20), ('3', 'B', 5);",
                        cancellationToken).ConfigureAwait(false);

                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText =
                            "SELECT RTRIM(GKey) AS GKey, COUNT(*) AS Cnt, SUM(Amount) AS S FROM G GROUP BY GKey HAVING COUNT(*) > 1";
                        await using var r = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        if (!await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                            exceptions.Add("groupby: expected 1 group row");
                        else
                        {
                            var gk = r.GetString(0).Trim();
                            var cnt = Convert.ToInt32(r.GetValue(1));
                            var sum = Convert.ToInt32(r.GetValue(2));
                            if (!string.Equals(gk, "A", StringComparison.OrdinalIgnoreCase) || cnt != 2 || sum != 30)
                                exceptions.Add($"groupby: unexpected row GKey={gk} Cnt={cnt} S={sum}");
                            if (await r.ReadAsync(cancellationToken).ConfigureAwait(false))
                                exceptions.Add("groupby: expected exactly 1 row");
                        }
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex.ToString());
                }

                sw.Stop();
                WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 group by", dbName, "LocalDbPhase4", "SetupAndQuery", exceptions);
                results.Add(Finish("Phase4 group by", exceptions.Count == 0, sw, 1, exceptions.Count == 0 ? 1 : 0, exceptions.Count, exceptions));
            }

            // --- subqueries ---
            {
                trace?.SetTestScope("Phase4 subqueries", "localdb");
                var sw = Stopwatch.StartNew();
                var exceptions = new List<string>();
                try
                {
                    logger?.Log("LocalDB Phase4 (single DB): subqueries");
                    await ExecuteNonQueryAsync(
                        conn,
                        @"CREATE TABLE Parent (Id CHAR(5) PRIMARY KEY);
                          CREATE TABLE Child (Id CHAR(5) PRIMARY KEY, PId CHAR(5), FOREIGN KEY (PId) REFERENCES Parent(Id));
                          INSERT INTO Parent (Id) VALUES ('1'), ('2');
                          INSERT INTO Child (Id, PId) VALUES ('c1', '1');",
                        cancellationToken).ConfigureAwait(false);

                    var inCnt = await ExecuteScalarIntAsync(
                        conn,
                        "SELECT COUNT(*) FROM Parent WHERE RTRIM(Id) IN (SELECT RTRIM(PId) FROM Child)",
                        cancellationToken).ConfigureAwait(false);
                    if (inCnt != 1)
                        exceptions.Add($"subquery IN: expected 1 row, got {inCnt}");
                    var inId = await ExecuteScalarStringAsync(
                        conn,
                        "SELECT RTRIM(Id) FROM Parent WHERE RTRIM(Id) IN (SELECT RTRIM(PId) FROM Child)",
                        cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(inId?.Trim(), "1", StringComparison.OrdinalIgnoreCase))
                        exceptions.Add($"subquery IN: expected Id 1, got {inId}");

                    var exCnt = await ExecuteScalarIntAsync(
                        conn,
                        "SELECT COUNT(*) FROM Parent p WHERE EXISTS (SELECT 1 FROM Child c WHERE RTRIM(c.PId) = RTRIM(p.Id))",
                        cancellationToken).ConfigureAwait(false);
                    if (exCnt != 1)
                        exceptions.Add($"subquery EXISTS: expected 1 row, got {exCnt}");

                    var scalarCnt = await ExecuteScalarStringAsync(
                        conn,
                        @"SELECT CAST((SELECT COUNT(*) FROM Child c WHERE RTRIM(c.PId) = RTRIM(p.Id)) AS VARCHAR(20))
                          FROM Parent p WHERE RTRIM(p.Id) = '1'",
                        cancellationToken).ConfigureAwait(false);
                    if (!string.Equals(scalarCnt?.Trim(), "1", StringComparison.OrdinalIgnoreCase))
                        exceptions.Add($"scalar COUNT: expected 1, got {scalarCnt}");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex.ToString());
                }

                sw.Stop();
                WriteLocalDbPhase4FailureBundle(trace, logger, "Phase4 subqueries", dbName, "LocalDbPhase4", "SetupAndQuery", exceptions);
                results.Add(Finish("Phase4 subqueries", exceptions.Count == 0, sw, 3, exceptions.Count == 0 ? 3 : 0, exceptions.Count, exceptions));
            }
        }
        finally
        {
            if (dbName is not null)
            {
                try
                {
                    await DropTempCompareDbAsync(dbName, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    /* best-effort */
                }
            }
        }

        return results;
    }
}
