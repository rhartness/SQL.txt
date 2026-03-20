namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Fan-out sink; thread-safe for concurrent manual test workers.
/// </summary>
public sealed class CompositeTestDiagnosticSink : ITestDiagnosticSink
{
    private readonly ITestDiagnosticSink[] _sinks;

    public CompositeTestDiagnosticSink(params ITestDiagnosticSink[] sinks) =>
        _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));

    public void Write(DiagnosticEventDto e)
    {
        foreach (var s in _sinks)
            s.Write(e);
    }

    public void Dispose()
    {
        foreach (var s in _sinks)
            s.Dispose();
    }
}
