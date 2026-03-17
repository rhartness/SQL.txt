namespace SqlTxt.Contracts;

/// <summary>
/// Options for building the sample Wiki database.
/// </summary>
/// <param name="Verbose">When true, return step-by-step messages (for CLI).</param>
/// <param name="DeleteIfExists">Delete existing database before creating (default: true).</param>
public sealed record BuildSampleWikiOptions(
    bool Verbose = false,
    bool DeleteIfExists = true);
