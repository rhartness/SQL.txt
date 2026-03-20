namespace SqlTxt.ManualTests.Diagnostics;

using SqlTxt.ManualTests.Results;

/// <summary>
/// Writes <see cref="FailureBundle"/> next to the primary log when a manual test fails (with or without --diagnostics).
/// </summary>
internal static class ManualTestFailureSupport
{
    /// <summary>
    /// Emits <c>.failure-bundle.json</c> beside the primary log when <paramref name="logger"/> or run context provides a log path.
    /// </summary>
    public static void WriteFailureIfEnabled(
        ManualTestTrace? trace,
        ResultLogger? logger,
        string testName,
        string? storageType,
        string? failedStage,
        string? failedStep,
        string message,
        IReadOnlyDictionary<string, string>? paths = null,
        IReadOnlyDictionary<string, string>? artifactHints = null,
        string? sqlSnippet = null)
    {
        var ctx = ManualTestRunContext.CurrentOrFallback;
        var logPath = logger?.LogPath ?? ctx?.PrimaryLogPath;
        if (string.IsNullOrEmpty(logPath))
            return;

        FailureBundle bundle;
        if (trace is not null)
        {
            bundle = trace.BuildFailureBundle(
                testName,
                storageType,
                failedStage,
                failedStep,
                message,
                paths,
                artifactHints,
                sqlSnippet);
        }
        else
        {
            bundle = new FailureBundle
            {
                RunId = ctx?.RunId ?? "",
                TestName = testName,
                StorageType = storageType,
                FailedStage = failedStage,
                FailedStep = failedStep,
                Message = message,
                Paths = paths is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(paths, StringComparer.OrdinalIgnoreCase),
                ArtifactHints = artifactHints is null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(artifactHints, StringComparer.OrdinalIgnoreCase),
                SqlSnippetRedacted = RedactSql(sqlSnippet),
                RecentEvents = null
            };
        }

        ManualTestRunContext.WriteFailureBundle(logPath, bundle);
    }

    private static string? RedactSql(string? sql)
    {
        if (string.IsNullOrEmpty(sql))
            return null;
        const int max = 4096;
        return sql.Length <= max ? sql : sql[..(max - 1)] + "…";
    }
}
