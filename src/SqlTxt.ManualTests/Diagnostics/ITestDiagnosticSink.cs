namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Receives structured diagnostic events from <see cref="ManualTestTrace"/>.
/// </summary>
public interface ITestDiagnosticSink : IDisposable
{
    void Write(DiagnosticEventDto e);
}
