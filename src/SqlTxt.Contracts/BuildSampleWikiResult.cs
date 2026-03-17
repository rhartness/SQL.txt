namespace SqlTxt.Contracts;

/// <summary>
/// Result of building the sample Wiki database.
/// </summary>
/// <param name="StatementsExecuted">Number of statements executed.</param>
/// <param name="Steps">Step-by-step messages (when Verbose).</param>
/// <param name="Warnings">Non-fatal warnings.</param>
public sealed record BuildSampleWikiResult(
    int StatementsExecuted,
    IReadOnlyList<string> Steps,
    IReadOnlyList<string> Warnings);
