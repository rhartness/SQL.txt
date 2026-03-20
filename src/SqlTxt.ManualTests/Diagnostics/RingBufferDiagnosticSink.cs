namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Keeps the last N events for failure bundles (thread-safe). N defaults to 200; use the constructor argument.
/// </summary>
public sealed class RingBufferDiagnosticSink : ITestDiagnosticSink
{
    private readonly int _capacity;
    private readonly object _lock = new();
    private readonly List<DiagnosticEventDto> _buffer = [];

    public RingBufferDiagnosticSink(int capacity = 200) =>
        _capacity = Math.Max(16, capacity);

    public void Write(DiagnosticEventDto e)
    {
        lock (_lock)
        {
            _buffer.Add(CloneEvent(e));
            while (_buffer.Count > _capacity)
                _buffer.RemoveAt(0);
        }
    }

    public IReadOnlyList<DiagnosticEventDto> Snapshot()
    {
        lock (_lock)
            return _buffer.Select(CloneEvent).ToList();
    }

    private static DiagnosticEventDto CloneEvent(DiagnosticEventDto e) =>
        new()
        {
            v = e.v,
            runId = e.runId,
            tsUtc = e.tsUtc,
            kind = e.kind,
            testName = e.testName,
            storageType = e.storageType,
            stage = e.stage,
            step = e.step,
            elapsedMs = e.elapsedMs,
            data = e.data is null ? null : new Dictionary<string, string>(e.data, StringComparer.Ordinal)
        };

    public void Dispose()
    {
    }
}
