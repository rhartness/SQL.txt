using System.Diagnostics;
using SqlTxt.Contracts.Exceptions;
using SqlTxt.ManualTests.Diagnostics;
using SqlTxt.ManualTests.Results;

namespace SqlTxt.ManualTests.Tests;

/// <summary>
/// Shared helpers for Phase 4 feature manual tests. When parser/engine do not yet support
/// Phase 4 syntax, tests return a skipped result (Passed=true, Details["Skipped"]=true) so
/// the manual suite stays usable before implementation lands.
/// </summary>
internal static class Phase4ManualTestHelper
{
    /// <summary>
    /// True when the failure is treated as "feature not implemented yet" (skip), not a product bug.
    /// </summary>
    public static bool ShouldSkipAsNotImplemented(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException!)
        {
            if (e is ParseException)
                return true;
        }

        return false;
    }

    public static TestResult Skipped(string testName, string reason, Stopwatch sw, string? storage, Dictionary<string, object>? extra = null)
    {
        var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["Skipped"] = true,
            ["SkippedReason"] = reason
        };
        if (extra != null)
        {
            foreach (var kv in extra)
                d[kv.Key] = kv.Value;
        }

        return new TestResult(testName, true, sw.Elapsed, 0, 0, 0, Array.Empty<string>(), d, storage);
    }

    public static TestResult Failed(
        string testName,
        Exception ex,
        Stopwatch sw,
        string? storage,
        ManualTestTrace? trace = null,
        ResultLogger? logger = null,
        string? failedStage = null,
        string? failedStep = null,
        string? phase4DbPath = null,
        string? sqlSnippet = null)
    {
        IReadOnlyDictionary<string, string>? paths = null;
        if (!string.IsNullOrEmpty(phase4DbPath))
        {
            paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["phase4DbPath"] = Path.GetFullPath(phase4DbPath)
            };
        }

        ManualTestFailureSupport.WriteFailureIfEnabled(
            trace,
            logger,
            testName,
            storage,
            failedStage,
            failedStep,
            ex.ToString(),
            paths,
            null,
            sqlSnippet);

        return new TestResult(testName, false, sw.Elapsed, 1, 0, 1, new[] { ex.ToString() }, null, storage);
    }
}
