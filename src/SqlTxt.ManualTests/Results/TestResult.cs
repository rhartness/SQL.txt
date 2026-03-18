namespace SqlTxt.ManualTests.Results;

/// <summary>
/// Result of a manual test run.
/// </summary>
/// <param name="TestName">Name of the test.</param>
/// <param name="Passed">Whether the test passed overall.</param>
/// <param name="Duration">Elapsed time.</param>
/// <param name="OperationsCount">Total operations attempted.</param>
/// <param name="SuccessCount">Successful operations.</param>
/// <param name="FailureCount">Failed operations.</param>
/// <param name="Exceptions">Exception messages collected.</param>
/// <param name="Details">Additional details (e.g., query timings).</param>
public sealed record TestResult(
    string TestName,
    bool Passed,
    TimeSpan Duration,
    int OperationsCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<string> Exceptions,
    IReadOnlyDictionary<string, object>? Details = null);
