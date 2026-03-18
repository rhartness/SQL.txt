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
        Log($"--- {result.TestName} ---");
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

    public void Dispose()
    {
        _writer.Dispose();
        GC.SuppressFinalize(this);
    }

    public string LogPath => _logPath;
}
