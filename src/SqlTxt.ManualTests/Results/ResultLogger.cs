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
        var status = result.Passed ? "PASSED" : "FAILED";
        var storageLabel = result.StorageType is not null ? $" [{result.StorageType}]" : "";
        Log($"--- {result.TestName}{storageLabel} ---");
        Log($"Status: {status}");
        Log($"Duration: {result.Duration.TotalMilliseconds:F2} ms");
        Log($"Operations: {result.OperationsCount} total, {result.SuccessCount} success, {result.FailureCount} failed");

        if (result.Exceptions.Count > 0)
        {
            Log($"Exceptions ({result.Exceptions.Count}):");
            foreach (var ex in result.Exceptions)
                Log($"  - {ex}");
        }

        if (result.Details != null)
        {
            foreach (var (key, value) in result.Details)
                Log($"  {key}: {value}");
        }

        Log(string.Empty);
    }

    /// <summary>
    /// Logs a comparison table of test results across storage types (text vs binary).
    /// </summary>
    public void LogComparisonTable(IReadOnlyList<TestResult> results)
    {
        if (results.Count == 0) return;

        const int colTest = 24;
        const int colStorage = 10;
        const int colStatus = 8;
        const int colDuration = 14;
        const int colOps = 10;
        const int colSuccess = 10;
        const int colFail = 8;

        var header = $"{"Test",-colTest} {"Storage",-colStorage} {"Status",-colStatus} {"Duration(ms)",-colDuration} {"Ops",-colOps} {"Success",-colSuccess} {"Fail",-colFail}";
        var separator = new string('-', colTest + colStorage + colStatus + colDuration + colOps + colSuccess + colFail + 6);

        Log("=== Results Comparison (text vs binary) ===");
        Log(header);
        Log(separator);

        foreach (var r in results)
        {
            var storage = r.StorageType ?? "-";
            var status = r.Passed ? "PASS" : "FAIL";
            var line = $"{r.TestName,-colTest} {storage,-colStorage} {status,-colStatus} {r.Duration.TotalMilliseconds,12:F2} {r.OperationsCount,8} {r.SuccessCount,8} {r.FailureCount,6}";
            Log(line);
        }

        Log(separator);
        Log(string.Empty);
    }

    public void Dispose()
    {
        _writer.Dispose();
        GC.SuppressFinalize(this);
    }

    public string LogPath => _logPath;
}
