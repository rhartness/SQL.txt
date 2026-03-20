using System.Text.Json;

namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Per-process manual test run: correlation id, JSONL path, and trace. Uses <see cref="AsyncLocal{T}"/> so async flows see the same context.
/// </summary>
public sealed class ManualTestRunContext : IDisposable
{
    private static readonly AsyncLocal<ManualTestRunContext?> AsyncCurrent = new();

    /// <summary>
    /// Fallback for worker threads that do not inherit <see cref="AsyncCurrent"/> (e.g. raw <c>Task.Run</c> in concurrency tests).
    /// </summary>
    private static ManualTestRunContext? _ambient;

    public static ManualTestRunContext? Current => AsyncCurrent.Value;

    public string RunId { get; }
    public string DiagnosticsJsonlPath { get; }
    /// <summary>Primary human log path for pairing failure-bundle and jsonl files.</summary>
    public string PrimaryLogPath { get; }
    public ManualTestTrace Trace { get; }
    private readonly CompositeTestDiagnosticSink _composite;
    private readonly RingBufferDiagnosticSink _ring;

    private ManualTestRunContext(string runId, string jsonlPath, string primaryLogPath, ManualTestTrace trace, CompositeTestDiagnosticSink composite, RingBufferDiagnosticSink ring)
    {
        RunId = runId;
        DiagnosticsJsonlPath = jsonlPath;
        PrimaryLogPath = primaryLogPath;
        Trace = trace;
        _composite = composite;
        _ring = ring;
    }

    /// <summary>
    /// Starts diagnostics for this run. Returns null when diagnostics are disabled.
    /// </summary>
    /// <param name="diagnosticsEnabled">When false, returns null and does not create files.</param>
    /// <param name="primaryLogPath">Path to the main .log file; JSONL is written alongside with a .diagnostics.jsonl suffix.</param>
    /// <param name="runId">Shared correlation id (log header, jsonl, Markdown).</param>
    public static ManualTestRunContext? TryStart(bool diagnosticsEnabled, string primaryLogPath, string runId)
    {
        if (!diagnosticsEnabled)
            return null;

        var fullLog = Path.GetFullPath(primaryLogPath);
        var dir = Path.GetDirectoryName(fullLog) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(fullLog);
        var jsonlPath = Path.Combine(dir, baseName + ".diagnostics.jsonl");

        var ring = new RingBufferDiagnosticSink(300);
        var fileSink = new JsonlTestDiagnosticSink(jsonlPath);
        var composite = new CompositeTestDiagnosticSink(ring, fileSink);
        var trace = new ManualTestTrace(composite, runId, ring);
        var ctx = new ManualTestRunContext(runId, jsonlPath, fullLog, trace, composite, ring);
        AsyncCurrent.Value = ctx;
        _ambient = ctx;
        trace.EmitRunStart();
        return ctx;
    }

    /// <summary>Resolves context on worker threads when <see cref="AsyncCurrent"/> is unset.</summary>
    public static ManualTestRunContext? CurrentOrFallback => AsyncCurrent.Value ?? _ambient;

    public void Dispose()
    {
        AsyncCurrent.Value = null;
        if (ReferenceEquals(_ambient, this))
            _ambient = null;
        _composite.Dispose();
    }

    /// <summary>
    /// Writes <c>&lt;basename&gt;.failure-bundle.json</c> next to the primary log.
    /// </summary>
    public void WriteFailureBundle(FailureBundle bundle) => WriteFailureBundle(PrimaryLogPath, bundle);

    public static void WriteFailureBundle(string primaryLogPath, FailureBundle bundle)
    {
        var fullLog = Path.GetFullPath(primaryLogPath);
        var dir = Path.GetDirectoryName(fullLog) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(fullLog);
        var path = Path.Combine(dir, baseName + ".failure-bundle.json");
        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
    }
}
