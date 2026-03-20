using System.Globalization;
using System.Text;

namespace SqlTxt.ManualTests.Results;

/// <summary>
/// Logs test results to console and file.
/// </summary>
public sealed class ResultLogger : IDisposable
{
    private readonly string _logPath;
    private readonly bool _verbose;
    private readonly StreamWriter _writer;

    public ResultLogger(string logPath, bool verbose = false, string? runId = null)
    {
        _logPath = logPath;
        _verbose = verbose;
        _writer = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
        if (!string.IsNullOrEmpty(runId))
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] RunId: {runId}";
            Console.WriteLine($"RunId: {runId}");
            _writer.WriteLine(line);
        }
    }

    public void Log(string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Console.WriteLine(message);
        _writer.WriteLine(line);
    }

    public void LogVerbose(string message)
    {
        if (_verbose)
            Log(message);
        else
            _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }

    public void LogResult(TestResult result)
    {
        var skipped = result.Details != null
            && result.Details.TryGetValue("Skipped", out var sk)
            && sk is true;
        var status = skipped ? "SKIPPED" : result.Passed ? "PASSED" : "FAILED";
        var storageLabel = result.StorageType is not null ? $" [{result.StorageType}]" : "";
        Log($"--- {result.TestName}{storageLabel} ---");
        Log($"Status: {status}");
        Log($"Total duration: {result.Duration.TotalMilliseconds:F2} ms");
        Log($"Operations: {result.OperationsCount} total, {result.SuccessCount} success, {result.FailureCount} failed");

        if (result.Details != null)
        {
            LogStepMetrics(result.Details);
            LogOtherDetails(result.Details);
        }

        if (result.Exceptions.Count > 0)
        {
            Log($"Exceptions ({result.Exceptions.Count}):");
            foreach (var ex in result.Exceptions)
                Log($"  - {ex}");
        }

        Log(string.Empty);
    }

    private void LogStepMetrics(IReadOnlyDictionary<string, object> details)
    {
        var stepKeys = details.Keys.Where(k => k.StartsWith("Step_", StringComparison.Ordinal) && k.EndsWith("_Ms", StringComparison.Ordinal)).ToList();
        if (stepKeys.Count == 0) return;

        Log("Step timings:");
        foreach (var key in stepKeys.OrderBy(k => k))
        {
            var stepName = key[5..^3].Replace("_", " ");
            var ms = details[key];
            var msStr = ms is double d ? $"{d:F2}" : ms is long l ? $"{l:F2}" : ms?.ToString() ?? "-";
            var countKey = key[..^3] + "_Count";
            var countStr = details.TryGetValue(countKey, out var c) && c is int cnt ? $" ({cnt} ops)" : "";
            Log($"  {stepName}: {msStr} ms{countStr}");
        }

        var avgKeys = details.Keys.Where(k => k.StartsWith("Avg_", StringComparison.Ordinal) && k.EndsWith("_Ms", StringComparison.Ordinal)).ToList();
        if (avgKeys.Count > 0)
        {
            Log("Averages (per operation):");
            foreach (var key in avgKeys.OrderBy(k => k))
            {
                var opName = key[4..^3].Replace("_", " ");
                var ms = details[key];
                var msStr = ms is double d ? $"{d:F2}" : ms is long l ? $"{l:F2}" : ms?.ToString() ?? "-";
                Log($"  {opName}: {msStr} ms avg");
            }
        }
    }

    private void LogOtherDetails(IReadOnlyDictionary<string, object> details)
    {
        var skipKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in details.Keys)
        {
            if (k.StartsWith("Step_", StringComparison.Ordinal) || k.StartsWith("Avg_", StringComparison.Ordinal))
                skipKeys.Add(k);
        }

        foreach (var (key, value) in details.Where(kv => !skipKeys.Contains(kv.Key)).OrderBy(kv => kv.Key))
            Log($"  {key}: {value}");
    }

    private static readonly string[] PreferredStorageColumnOrder = { "text", "binary", "localdb" };

    /// <summary>Wide enough for a parenthesized total like (12345) while keeping Avg/Ops columns aligned.</summary>
    private const int SummaryMetricTotalW = 12;
    private const int SummaryMetricAvgW = 9;
    private const int SummaryMetricOpsW = 6;

    private static int SummaryGroupInnerWidth() =>
        SummaryMetricTotalW + 3 + SummaryMetricAvgW + 3 + SummaryMetricOpsW;

    /// <summary>Inner metric block plus a leading and trailing space for readability.</summary>
    private static int SummaryGroupDisplayWidth() => SummaryGroupInnerWidth() + 2;

    /// <summary>
    /// Logs a pivot table: one row per test name; per storage, columns Total / Avg / Ops.
    /// Two header rows group storage names over metrics. The fastest passing run(s) per row parenthesize only Total (ms) (lowest total ms; needs 2+ comparable backends).
    /// </summary>
    public void LogSummaryTable(IReadOnlyList<TestResult> results)
    {
        if (results.Count == 0) return;

        const int colTest = 26;
        var gW = SummaryGroupDisplayWidth();

        var storageCols = DiscoverStorageColumns(results);
        if (storageCols.Count == 0)
        {
            LogLegacyLongSummary(results, colTest);
            return;
        }

        var testNames = new List<string>();
        foreach (var r in results)
        {
            if (!testNames.Contains(r.TestName, StringComparer.OrdinalIgnoreCase))
                testNames.Add(r.TestName);
        }

        var byTestAndStorage = new Dictionary<string, TestResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            var st = r.StorageType;
            if (string.IsNullOrEmpty(st)) continue;
            byTestAndStorage[PivotKey(r.TestName, st)] = r;
        }

        var lineWidth = colTest + 3 + storageCols.Count * (gW + 3) - 3;
        var separator = new string('-', Math.Min(lineWidth, 200));

        Log(string.Empty);
        Log("=== Results Summary ===");
        Log("Grouped columns: Total (ms), Avg/op (ms), Ops (Avg/Ops use details + step counts when Avg_* keys missing). Parentheses on Total = lowest total ms among passing runs (2+ backends). FAIL = storage run failed.");
        Log(separator);

        var groupW = gW;
        var headerGroup = new List<string> { Truncate("Test", colTest).PadRight(colTest) };
        foreach (var st in storageCols)
            headerGroup.Add(CenterText(st, groupW));
        Log(string.Join(" | ", headerGroup));

        var subParts = new List<string> { new string(' ', colTest) };
        foreach (var _ in storageCols)
        {
            var t = Truncate("Total", SummaryMetricTotalW).PadRight(SummaryMetricTotalW);
            var a = Truncate("Avg", SummaryMetricAvgW).PadRight(SummaryMetricAvgW);
            var o = Truncate("Ops", SummaryMetricOpsW).PadRight(SummaryMetricOpsW);
            subParts.Add($"{t} | {a} | {o}".PadRight(groupW));
        }

        Log(string.Join(" | ", subParts));
        Log(separator);

        foreach (var testName in testNames)
        {
            var parts = new List<SummaryCellParts>();
            foreach (var st in storageCols)
            {
                byTestAndStorage.TryGetValue(PivotKey(testName, st), out var cell);
                parts.Add(ToSummaryCellParts(cell));
            }

            var best = BestStorageIndicesForTotals(parts);
            var rowList = new List<string> { Truncate(testName, colTest).PadRight(colTest) };
            for (var i = 0; i < storageCols.Count; i++)
                rowList.Add(FormatSummaryStorageGroup(parts[i], best.Contains(i)).PadRight(groupW));

            Log(string.Join(" | ", rowList));
        }

        Log(separator);
        if (results.Count > 1)
        {
            var footerParts = new List<SummaryCellParts>();
            foreach (var st in storageCols)
            {
                var sum = results
                    .Where(r => string.Equals(r.StorageType, st, StringComparison.OrdinalIgnoreCase))
                    .Sum(r => r.Duration.TotalMilliseconds);
                footerParts.Add(new SummaryCellParts(
                    Truncate($"{sum:F0}", SummaryMetricTotalW),
                    "—",
                    "—",
                    sum,
                    true));
            }

            var bestFoot = BestStorageIndicesForTotals(footerParts);
            var totalRow = new List<string> { "TOTAL ms".PadRight(colTest) };
            for (var i = 0; i < storageCols.Count; i++)
                totalRow.Add(FormatSummaryStorageGroup(footerParts[i], bestFoot.Contains(i)).PadRight(groupW));

            Log(string.Join(" | ", totalRow));
            Log(separator);
        }

        Log(string.Empty);
    }

    private readonly record struct SummaryCellParts(
        string Total,
        string Avg,
        string Ops,
        double CompareTotalMs,
        bool EligibleForBest);

    private static SummaryCellParts ToSummaryCellParts(TestResult? r)
    {
        if (r is null)
            return new SummaryCellParts("—", "—", "—", double.NaN, false);
        if (IsSkippedResult(r))
            return new SummaryCellParts("SKIP", "—", "—", double.NaN, false);

        var totalMs = r.Duration.TotalMilliseconds;
        var totalStr = $"{totalMs:F0}";
        if (!r.Passed)
            totalStr = Truncate(totalStr + " FAIL", SummaryMetricTotalW);
        var avg = GetSummaryAvgMs(r);
        var avgStr = avg > 0 ? $"{avg:F2}" : "—";
        var ops = TryGetSummaryOpsCount(r);
        var opsStr = ops is > 0 ? ops.Value.ToString(CultureInfo.InvariantCulture) : "—";
        var eligible = r.Passed && !IsSkippedResult(r);
        return new SummaryCellParts(
            Truncate(totalStr, SummaryMetricTotalW),
            Truncate(avgStr, SummaryMetricAvgW),
            Truncate(opsStr, SummaryMetricOpsW),
            totalMs,
            eligible);
    }

    private static HashSet<int> BestStorageIndicesForTotals(IReadOnlyList<SummaryCellParts> parts)
    {
        var candidates = new List<(int Index, double Ms)>();
        for (var i = 0; i < parts.Count; i++)
        {
            var p = parts[i];
            if (!p.EligibleForBest || double.IsNaN(p.CompareTotalMs)) continue;
            candidates.Add((i, p.CompareTotalMs));
        }

        if (candidates.Count < 2)
            return new HashSet<int>();

        var best = candidates.Min(c => c.Ms);
        return candidates.Where(c => Math.Abs(c.Ms - best) < 0.0001).Select(c => c.Index).ToHashSet();
    }

    private static string FormatSummaryStorageGroup(SummaryCellParts p, bool wrap)
    {
        var tot = wrap ? $"({p.Total})" : p.Total;
        tot = Truncate(tot, SummaryMetricTotalW);
        var t = tot.PadLeft(SummaryMetricTotalW);
        var a = Truncate(p.Avg, SummaryMetricAvgW).PadLeft(SummaryMetricAvgW);
        var o = Truncate(p.Ops, SummaryMetricOpsW).PadLeft(SummaryMetricOpsW);
        var core = $"{t} | {a} | {o}";
        return $" {core} ".PadRight(SummaryGroupDisplayWidth());
    }

    private static string CenterText(string text, int width)
    {
        var s = Truncate(text, width);
        if (s.Length >= width) return s;
        var pad = width - s.Length;
        var left = pad / 2;
        return s.PadLeft(s.Length + left).PadRight(width);
    }

    private static string PivotKey(string testName, string storage) =>
        $"{testName}\0{storage.ToLowerInvariant()}";

    private static List<string> DiscoverStorageColumns(IReadOnlyList<TestResult> results)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            if (!string.IsNullOrEmpty(r.StorageType))
                set.Add(r.StorageType.ToLowerInvariant());
        }

        var list = new List<string>();
        foreach (var p in PreferredStorageColumnOrder)
        {
            if (set.Remove(p))
                list.Add(p);
        }

        list.AddRange(set.OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
        return list;
    }

    private static bool IsSkippedResult(TestResult r) =>
        r.Details != null && r.Details.TryGetValue("Skipped", out var sk) && sk is true;

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..(maxLen - 1)] + "…";

    private void LogLegacyLongSummary(IReadOnlyList<TestResult> results, int colTest)
    {
        Log("--- (no StorageType set; listing runs in order) ---");
        foreach (var r in results)
        {
            var avg = GetSummaryAvgMs(r);
            var ops = TryGetSummaryOpsCount(r);
            var opsStr = ops is > 0 ? ops.Value.ToString(CultureInfo.InvariantCulture) : "—";
            var avgStr = avg > 0 ? $"{avg:F2}" : "—";
            Log($"{Truncate(r.TestName, colTest).PadRight(colTest)} | {r.Duration.TotalMilliseconds:F0} / {avgStr} / {opsStr}");
        }
    }

    /// <summary>
    /// Summary "Avg/op" column: prefers explicit <c>Avg_*_Ms</c> details, then per-step ms/count from <c>Step_*</c>,
    /// then wall time divided by <see cref="TryGetSummaryOpsCount"/> when that count is &gt; 0.
    /// </summary>
    private static double GetSummaryAvgMs(TestResult r)
    {
        var fromAvgKeys = GetPrimaryAvgMs(r);
        if (fromAvgKeys > 0)
            return fromAvgKeys;

        var stepAvg = GetFirstStepAverageMs(r);
        if (stepAvg > 0)
            return stepAvg;

        var ops = TryGetSummaryOpsCount(r);
        if (ops is > 0 && r.Duration.TotalMilliseconds > 0)
            return r.Duration.TotalMilliseconds / ops.Value;

        return 0;
    }

    /// <summary>
    /// Ops column: <see cref="TestResult.OperationsCount"/> if set, otherwise sums <c>Step_*_Count</c> from details.
    /// </summary>
    private static int? TryGetSummaryOpsCount(TestResult r)
    {
        if (r.OperationsCount > 0)
            return r.OperationsCount;
        if (r.Details is null)
            return null;

        var sum = 0;
        var any = false;
        foreach (var k in r.Details.Keys)
        {
            if (!k.StartsWith("Step_", StringComparison.Ordinal) || !k.EndsWith("_Count", StringComparison.Ordinal))
                continue;
            if (r.Details[k] is int c && c > 0)
            {
                sum += c;
                any = true;
            }
        }

        return any ? sum : null;
    }

    /// <summary>Returns ms/count for the first Step_* pair with positive count and ms.</summary>
    private static double GetFirstStepAverageMs(TestResult r)
    {
        if (r.Details is null)
            return 0;

        foreach (var msKey in r.Details.Keys
                     .Where(k => k.StartsWith("Step_", StringComparison.Ordinal) && k.EndsWith("_Ms", StringComparison.Ordinal))
                     .OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
        {
            var countKey = msKey[..^3] + "_Count";
            if (!r.Details.TryGetValue(countKey, out var cobj) || cobj is not int cnt || cnt <= 0)
                continue;
            if (!r.Details.TryGetValue(msKey, out var msobj))
                continue;
            var ms = msobj is double d ? d : msobj is long l ? (double)l : 0;
            if (ms > 0)
                return ms / cnt;
        }

        return 0;
    }

    private static double GetPrimaryAvgMs(TestResult r)
    {
        if (r.Details == null) return 0;
        var avgKeys = r.Details.Keys
            .Where(k => k.StartsWith("Avg_", StringComparison.Ordinal) && k.EndsWith("_Ms", StringComparison.Ordinal))
            .OrderBy(k => k)
            .ToList();
        if (avgKeys.Count == 0) return 0;
        var values = avgKeys
            .Select(k => r.Details[k])
            .Select(v => v is double d ? d : v is long l ? (double)l : 0)
            .Where(x => x > 0)
            .ToList();
        return values.Count > 0 ? values.Average() : 0;
    }

    /// <summary>
    /// Logs a comparison table of test results across storage types (text vs binary).
    /// </summary>
    public void LogComparisonTable(IReadOnlyList<TestResult> results)
    {
        if (results.Count == 0) return;
        LogSummaryTable(results);
    }

    public void Dispose()
    {
        _writer.Dispose();
        GC.SuppressFinalize(this);
    }

    public string LogPath => _logPath;
}
