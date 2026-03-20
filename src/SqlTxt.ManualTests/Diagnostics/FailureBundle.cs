namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Serializable artifact for agents (Cursor): failure context, paths, and recent diagnostic events.
/// </summary>
public sealed class FailureBundle
{
    public string SchemaVersion { get; init; } = "1.0";

    public string RunId { get; init; } = "";

    public string TestName { get; init; } = "";

    public string? StorageType { get; init; }

    public string? FailedStage { get; init; }

    public string? FailedStep { get; init; }

    public string Message { get; init; } = "";

    public Dictionary<string, string> Paths { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> ArtifactHints { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public string? SqlSnippetRedacted { get; init; }

    public IReadOnlyList<Dictionary<string, string>>? RecentEvents { get; init; }
}
