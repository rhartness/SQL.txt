using System.Globalization;
using System.Text;

namespace SqlTxt.ManualTests.Results;

/// <summary>
/// Writes a secondary, reference-friendly Markdown report next to the main manual test log:
/// failures (with anchors), SqlTxt vs comparator timings, application deficits, and slower-than-baseline lists.
/// </summary>
public static class ManualTestIssuesReport
{
    public const string FileSuffix = ".errors-and-comparison.md";

    public static string WriteMarkdown(string primaryLogPath, IReadOnlyList<TestResult> results, out bool sqlTxtSlowerThanLocalDb) =>
        WriteMarkdown(primaryLogPath, results, null, null, out sqlTxtSlowerThanLocalDb);

    /// <summary>
    /// Writes the report. <paramref name="comparators"/> defaults to <see cref="ManualTestComparator.DefaultSuite"/> when null.
    /// </summary>
    public static string WriteMarkdown(
        string primaryLogPath,
        IReadOnlyList<TestResult> results,
        string? runId,
        IReadOnlyList<ManualTestComparator>? comparators,
        out bool sqlTxtSlowerThanLocalDb)
    {
        var fullLog = Path.GetFullPath(primaryLogPath);
        sqlTxtSlowerThanLocalDb = false;
        comparators ??= ManualTestComparator.DefaultSuite;
        var dir = Path.GetDirectoryName(fullLog) ?? ".";
        var name = Path.GetFileNameWithoutExtension(fullLog);
        var path = Path.Combine(dir, name + FileSuffix);

        static bool Skipped(TestResult r) =>
            r.Details != null && r.Details.TryGetValue("Skipped", out var sk) && sk is true;

        static bool Comparable(TestResult r) => r.Passed && !Skipped(r);

        static string FmtMs(double ms) => ms.ToString("F2", CultureInfo.InvariantCulture);

        static string FmtRatio(double sqlMs, double baseMs) =>
            baseMs > 0 ? (sqlMs / baseMs).ToString("F2", CultureInfo.InvariantCulture) : "—";

        var byTest = new Dictionary<string, List<TestResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            if (!byTest.TryGetValue(r.TestName, out var list))
            {
                list = [];
                byTest[r.TestName] = list;
            }

            list.Add(r);
        }

        var anySqlTxtSlowerThanBaseline = false;

        var sb = new StringBuilder();
        sb.AppendLine("# Manual tests — errors and SqlTxt vs baseline(s)");
        sb.AppendLine();
        sb.AppendLine("Use section anchors: `#failures`, `#vs-localdb`, `#slower-than-localdb`, `#deficits`.");
        sb.AppendLine();
        sb.AppendLine($"- **UTC:** `{DateTime.UtcNow:O}`");
        sb.AppendLine($"- **Primary log:** `{fullLog}`");
        if (!string.IsNullOrEmpty(runId))
            sb.AppendLine($"- **RunId:** `{runId}`");
        sb.AppendLine($"- **Comparators:** {string.Join(", ", comparators.Select(c => $"`{c.DisplayName}` ({c.StorageLabel})"))}");
        sb.AppendLine();

        sb.AppendLine("## Failures {#failures}");
        var failures = results.Where(r => !r.Passed).ToList();
        if (failures.Count == 0)
            sb.AppendLine("*No failing runs.*");
        else
        {
            foreach (var r in failures)
            {
                var st = r.StorageType ?? "(unspecified)";
                sb.AppendLine($"- **{r.TestName}** [`{st}`] — duration {FmtMs(r.Duration.TotalMilliseconds)} ms");
                foreach (var ex in r.Exceptions)
                    sb.AppendLine($"  - {ex.ReplaceLineEndings(" ").Trim()}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## SqlTxt vs baseline (wall time) {#vs-localdb}");
        sb.AppendLine();
        sb.AppendLine("| Test | text ms | binary ms | baseline ms | text vs | binary vs |");
        sb.AppendLine("|------|---------|-----------|-------------|---------|-----------|");

        var slowerLines = new List<string>();
        var deficitRows = new List<string>();

        foreach (var testName in byTest.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var list = byTest[testName];
            TestResult? T(string storage) =>
                list.FirstOrDefault(x => string.Equals(x.StorageType, storage, StringComparison.OrdinalIgnoreCase));

            var text = T("text");
            var binary = T("binary");
            var baseline = comparators.Count > 0 ? T(comparators[0].StorageLabel) : null;

            var tMs = text is not null ? FmtMs(text.Duration.TotalMilliseconds) : "—";
            var bMs = binary is not null ? FmtMs(binary.Duration.TotalMilliseconds) : "—";
            var blMs = baseline is not null ? FmtMs(baseline.Duration.TotalMilliseconds) : "—";

            string Vs(TestResult? sqlTxt, ManualTestComparator comp, TestResult? baseRun)
            {
                if (sqlTxt is null || baseRun is null)
                    return "—";
                if (!Comparable(sqlTxt) || !Comparable(baseRun))
                    return "n/a";
                var st = sqlTxt.Duration.TotalMilliseconds;
                var bs = baseRun.Duration.TotalMilliseconds;
                if (st > bs)
                {
                    anySqlTxtSlowerThanBaseline = true;
                    slowerLines.Add(
                        $"- `{testName}` **{sqlTxt.StorageType}** {FmtMs(st)} ms > {comp.DisplayName} {FmtMs(bs)} ms (+{FmtMs(st - bs)} ms)");
                    var ctx = BuildContextHint(sqlTxt);
                    deficitRows.Add(
                        $"| {testName} | {comp.DisplayName} | {sqlTxt.StorageType} | {FmtMs(bs)} | {FmtMs(st)} | +{FmtMs(st - bs)} | {FmtRatio(st, bs)} | {ctx} |");
                    return $"SLOWER (+{FmtMs(st - bs)})";
                }

                if (st < bs)
                    return $"faster (−{FmtMs(bs - st)})";

                return "tie";
            }

            var vs0 = comparators.Count > 0 ? Vs(text, comparators[0], baseline) : "—";
            var vs1 = comparators.Count > 0 ? Vs(binary, comparators[0], baseline) : "—";

            sb.AppendLine($"| {testName} | {tMs} | {bMs} | {blMs} | {vs0} | {vs1} |");
        }

        sb.AppendLine();
        sb.AppendLine("### Passed SqlTxt slower than baseline {#slower-than-localdb}");
        if (slowerLines.Count == 0)
            sb.AppendLine("*None (or baseline/SqlTxt not all comparable for a test).*");
        else
        {
            foreach (var line in slowerLines)
                sb.AppendLine(line);
        }

        sb.AppendLine();
        sb.AppendLine("## Application deficits (performance) {#deficits}");
        sb.AppendLine();
        sb.AppendLine("SqlTxt runs that **passed** but took **longer** than the named comparator (same test). Ratio = SqlTxt ms ÷ baseline ms.");
        sb.AppendLine();
        sb.AppendLine("| Test | Comparator | SqlTxt | baseline ms | SqlTxt ms | Delta ms | Ratio | Context |");
        sb.AppendLine("|------|------------|--------|-------------|-----------|----------|-------|---------|");
        if (deficitRows.Count == 0)
            sb.AppendLine("| *No deficits rows (no slower passing SqlTxt vs baseline).* | | | | | | | |");
        else
        {
            foreach (var row in deficitRows)
                sb.AppendLine(row);
        }

        sb.AppendLine();
        sb.AppendLine("### Note on concurrency and LocalDB {#deficits-concurrency-note}");
        sb.AppendLine();
        sb.AppendLine("For **High Concurrency**, SqlTxt is file-backed with locking; LocalDB is an in-process engine. Large wall-time ratios here are **informational** (correctness/behavior parity), not a throughput regression signal, unless you opt into `--require-beat-localdb` / `--fail-on-deficit-ratio`.");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("*End of issues report.*");

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        sqlTxtSlowerThanLocalDb = anySqlTxtSlowerThanBaseline;
        return path;
    }

    /// <summary>
    /// True when any passing SqlTxt text/binary run has duration &gt; baseline × <paramref name="maxRatio"/> for the first comparator that has a baseline row.
    /// </summary>
    public static bool AnyDeficitExceedsRatio(
        IReadOnlyList<TestResult> results,
        double maxRatio,
        IReadOnlyList<ManualTestComparator>? comparators = null)
    {
        comparators ??= ManualTestComparator.DefaultSuite;
        if (comparators.Count == 0 || maxRatio <= 0)
            return false;

        static bool Skipped(TestResult r) =>
            r.Details != null && r.Details.TryGetValue("Skipped", out var sk) && sk is true;

        static bool Comparable(TestResult r) => r.Passed && !Skipped(r);

        var byTest = new Dictionary<string, List<TestResult>>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            if (!byTest.TryGetValue(r.TestName, out var list))
            {
                list = [];
                byTest[r.TestName] = list;
            }

            list.Add(r);
        }

        var comp = comparators[0];
        foreach (var list in byTest.Values)
        {
            TestResult? T(string storage) =>
                list.FirstOrDefault(x => string.Equals(x.StorageType, storage, StringComparison.OrdinalIgnoreCase));

            var baseline = T(comp.StorageLabel);
            if (baseline is null || !Comparable(baseline))
                continue;
            var bs = baseline.Duration.TotalMilliseconds;
            if (bs <= 0)
                continue;

            foreach (var storage in new[] { "text", "binary" })
            {
                var sql = T(storage);
                if (sql is null || !Comparable(sql))
                    continue;
                var st = sql.Duration.TotalMilliseconds;
                if (st > bs * maxRatio)
                    return true;
            }
        }

        return false;
    }

    private static string BuildContextHint(TestResult r)
    {
        if (r.Details is null || r.Details.Count == 0)
            return "—";
        var parts = new List<string>();
        if (r.Details.TryGetValue("RowCount", out var rc))
            parts.Add($"rows={rc}");
        if (r.Details.TryGetValue("ShardCount", out var sc))
            parts.Add($"shards={sc}");
        if (r.OperationsCount > 0)
            parts.Add($"ops={r.OperationsCount}");
        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }
}
