using System.Text;

namespace SqlTxt.ManualTests.Results;

/// <summary>
/// Logs test results to console and file.
/// </summary>
public sealed class ResultLogger : IDisposable
{
    private readonly string _logPath;
    private readonly bool _verbose;
    private readonly StreamWriter _writer;

    public ResultLogger(string logPath, bool verbose = false)
    {
        _logPath = logPath;
        _verbose = verbose;
        _writer = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
    }

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Console.WriteLine(message);
        _writer.WriteLine(line);
    }

    public void LogVerbose(string message)
    {
        if (_verbose)
            Log(message);
        else
            _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }

    public void LogResult(TestResult result)
    {
        var skipped = result.Details != null
            && result.Details.TryGetValue("Skipped", out var sk)
            && sk is true;
        var status = skipped ? "SKIPPED" : result.Passed ? "PASSED" : "FAILED";
        var storageLabel = result.StorageType is not null ? $" [{result.StorageType}]" : "";
        Log($"--- {result.TestName}{storageLabel} ---");
        Log($"Status: {status}");
        Log($"Total duration: {result.Duration.TotalMilliseconds:F2} ms");
        Log($"Operations: {result.OperationsCount} total, {result.SuccessCount} success, {result.FailureCount} failed");

        if (result.Details != null)
        {
            LogStepMetrics(result.Details);
            LogOtherDetails(result.Details);
        }

        if (result.Exceptions.Count > 0)
        {
            Log($"Exceptions ({result.Exceptions.Count}):");
            foreach (var ex in result.Exceptions)
                Log($"  - {ex}");
        }

        Log(string.Empty);
    }

    private void LogStepMetrics(IReadOnlyDictionary<string, object> details)
    {
        var stepKeys = details.Keys.Where(k => k.StartsWith("Step_", StringComparison.Ordinal) && k.EndsWith("_Ms", StringComparison.Ordinal)).ToList();
        if (stepKeys.Count == 0) return;

        Log("Step timings:");
        foreach (var key in stepKeys.OrderBy(k => k))
        {
            var stepName = key[5..^3].Replace("_", " ");
            var ms = details[key];
            var msStr = ms is double d ? $"{d:F2}" : ms is long l ? $"{l:F2}" : ms?.ToString() ?? "-";
            var countKey = key[..^3] + "_Count";
            var countStr = details.TryGetValue(countKey, out var c) && c is int cnt ? $" ({cnt} ops)" : "";
            Log($"  {stepName}: {msStr} ms{countStr}");
        }

        var avgKeys = details.Keys.Where(k => k.StartsWith("Avg_", StringComparison.Ordinal) && k.EndsWith("_Ms", StringComparison.Ordinal)).ToList();
        if (avgKeys.Count > 0)
        {
            Log("Averages (per operation):");
            foreach (var key in avgKeys.OrderBy(k => k))
            {
                var opName = key[4..^3].Replace("_", " ");
                var ms = details[key];
                var msStr = ms is double d ? $"{d:F2}" : ms is long l ? $"{l:F2}" : ms?.ToString() ?? "-";
                Log($"  {opName}: {msStr} ms avg");
            }
        }
    }

    private void LogOtherDetails(IReadOnlyDictionary<string, object> details)
    {
        var skipKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in details.Keys)
        {
            if (k.StartsWith("Step_", StringComparison.Ordinal) || k.StartsWith("Avg_", StringComparison.Ordinal))
                skipKeys.Add(k);
        }

        foreach (var (key, value) in details.Where(kv => !skipKeys.Contains(kv.Key)).OrderBy(kv => kv.Key))
            Log($"  {key}: {value}");
    }

    /// <summary>
    /// Logs a summarized fixed-width table: one line per test/storage pair.
    /// Columns: Test Run, Total (ms), Avg/op (ms), Exec (ms).
    /// </summary>
    public void LogSummaryTable(IReadOnlyList<TestResult> results)
    {
        if (results.Count == 0) return;

        const int colTestRun = 28;
        const int colTotal = 14;
        const int colAvgOp = 14;
        const int colExec = 14;
        const int totalWidth = colTestRun + colTotal + colAvgOp + colExec + 9;

        var header = $"{"Test Run",-colTestRun} | {"Total (ms)",colTotal} | {"Avg/op (ms)",colAvgOp} | {"Exec (ms)",colExec}";
        var separator = new string('-', totalWidth);

        Log(string.Empty);
        Log("=== Results Summary ===");
        Log(separator);
        Log(header);
        Log(separator);

        foreach (var r in results)
        {
            var skipped = r.Details != null && r.Details.TryGetValue("Skipped", out var sk) && sk is true;
            var testRun = r.StorageType is not null ? $"{r.TestName} [{r.StorageType}]" : r.TestName;
            if (skipped)
                testRun += " (skip)";
            var totalMs = r.Duration.TotalMilliseconds;
            var avgOpMs = GetPrimaryAvgMs(r);
            var execMs = totalMs;

            var line = $"{testRun,-colTestRun} | {totalMs,colTotal:F2} | {avgOpMs,colAvgOp:F2} | {execMs,colExec:F2}";
            Log(line);
        }

        Log(separator);
        if (results.Count > 1)
        {
            var grandTotal = results.Sum(r => r.Duration.TotalMilliseconds);
            var totalLine = $"{"TOTAL",-colTestRun} | {grandTotal,colTotal:F2} | {"-",colAvgOp} | {grandTotal,colExec:F2}";
            Log(totalLine);
            Log(separator);
        }
        Log(string.Empty);
    }

    private static double GetPrimaryAvgMs(TestResult r)
    {
        if (r.Details == null) return 0;
        var avgKeys = r.Details.Keys
            .Where(k => k.StartsWith("Avg_", StringComparison.Ordinal) && k.EndsWith("_Ms", StringComparison.Ordinal))
            .OrderBy(k => k)
            .ToList();
        if (avgKeys.Count == 0) return 0;
        var values = avgKeys
            .Select(k => r.Details[k])
            .Select(v => v is double d ? d : v is long l ? (double)l : 0)
            .Where(x => x > 0)
            .ToList();
        return values.Count > 0 ? values.Average() : 0;
    }

    /// <summary>
    /// Logs a comparison table of test results across storage types (text vs binary).
    /// </summary>
    public void LogComparisonTable(IReadOnlyList<TestResult> results)
    {
        if (results.Count == 0) return;
        LogSummaryTable(results);
    }

    public void Dispose()
    {
        _writer.Dispose();
        GC.SuppressFinalize(this);
    }

    public string LogPath => _logPath;
}
