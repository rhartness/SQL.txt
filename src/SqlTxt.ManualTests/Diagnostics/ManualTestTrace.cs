using System.Diagnostics;

namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Opt-in stage/step and counter logging for manual tests. Thread-safe emit; stage stack is protected for concurrent workers.
/// </summary>
public sealed class ManualTestTrace
{
    private const int MaxIterationErrorSamples = 50;
    private const int MaxContextValueLen = 512;

    private readonly ITestDiagnosticSink _sink;
    private readonly string _runId;
    private readonly RingBufferDiagnosticSink? _ring;
    private readonly object _gate = new();
    private readonly Stack<string> _stageStack = new();
    private string? _testName;
    private string? _storageType;
    private readonly List<string> _iterationErrorSamples = [];

    public ManualTestTrace(ITestDiagnosticSink sink, string runId, RingBufferDiagnosticSink? ring = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _runId = runId ?? throw new ArgumentNullException(nameof(runId));
        _ring = ring;
    }

    public void SetTestScope(string testName, string? storageType)
    {
        lock (_gate)
        {
            _testName = testName;
            _storageType = storageType;
        }
    }

    public void EmitRunStart() => Write("runStart", null, null, null, null);

    public StageScope BeginStage(string stage)
    {
        lock (_gate)
            _stageStack.Push(stage);
        Write("stageStart", stage, null, null, null);
        return new StageScope(this, stage, Stopwatch.StartNew());
    }

    internal void EndStage(string stage, double elapsedMs)
    {
        Write("stageEnd", stage, null, elapsedMs, null);
        lock (_gate)
        {
            if (_stageStack.Count > 0 && string.Equals(_stageStack.Peek(), stage, StringComparison.Ordinal))
                _stageStack.Pop();
        }
    }

    public StepScope BeginStep(string step, IReadOnlyDictionary<string, string>? context = null)
    {
        string? currentStage;
        lock (_gate)
            currentStage = _stageStack.Count > 0 ? _stageStack.Peek() : null;
        var data = NormalizeContext(context);
        Write("stepStart", currentStage, step, null, data);
        return new StepScope(this, currentStage, step, Stopwatch.StartNew());
    }

    internal void EndStep(string? stage, string step, double elapsedMs) =>
        Write("stepEnd", stage, step, elapsedMs, null);

    public void RecordCounter(string name, long value, IReadOnlyDictionary<string, string>? context = null)
    {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["counter"] = name, ["value"] = value.ToString() };
        if (context is not null)
        {
            foreach (var kv in context)
                data["ctx_" + kv.Key] = TruncateValue(kv.Value);
        }

        Write("counter", PeekStage(), null, null, data);
    }

    public void RecordIterationError(string detail)
    {
        var d = TruncateValue(detail, 500);
        lock (_gate)
        {
            if (_iterationErrorSamples.Count < MaxIterationErrorSamples)
                _iterationErrorSamples.Add(d);
        }

        Write("iterationError", PeekStage(), null, null, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["detail"] = d });
    }

    public IReadOnlyList<string> GetIterationErrorSamples()
    {
        lock (_gate)
            return _iterationErrorSamples.ToArray();
    }

    public FailureBundle BuildFailureBundle(
        string testName,
        string? storageType,
        string? failedStage,
        string? failedStep,
        string message,
        IReadOnlyDictionary<string, string>? paths = null,
        IReadOnlyDictionary<string, string>? artifactHints = null,
        string? sqlSnippet = null)
    {
        var recent = new List<Dictionary<string, string>>();
        if (_ring is not null)
        {
            foreach (var e in _ring.Snapshot())
                recent.Add(EventToRow(e));
        }

        return new FailureBundle
        {
            RunId = _runId,
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
            RecentEvents = recent
        };
    }

    private string? PeekStage()
    {
        lock (_gate)
            return _stageStack.Count > 0 ? _stageStack.Peek() : null;
    }

    private void Write(string kind, string? stage, string? step, double? elapsedMs, Dictionary<string, string>? data)
    {
        string? tn;
        string? st;
        lock (_gate)
        {
            tn = _testName;
            st = _storageType;
        }

        _sink.Write(new DiagnosticEventDto
        {
            v = 1,
            runId = _runId,
            tsUtc = DateTime.UtcNow.ToString("O"),
            kind = kind,
            testName = tn,
            storageType = st,
            stage = stage,
            step = step,
            elapsedMs = elapsedMs,
            data = data
        });
    }

    private static Dictionary<string, string>? NormalizeContext(IReadOnlyDictionary<string, string>? context)
    {
        if (context is null || context.Count == 0)
            return null;
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in context)
            d[kv.Key] = TruncateValue(kv.Value);
        return d;
    }

    private static string TruncateValue(string value, int maxLen = MaxContextValueLen) =>
        value.Length <= maxLen ? value : value[..(maxLen - 1)] + "…";

    private static string? RedactSql(string? sql)
    {
        if (string.IsNullOrEmpty(sql)) return null;
        const int max = 4096;
        return sql.Length <= max ? sql : sql[..(max - 1)] + "…";
    }

    private static Dictionary<string, string> EventToRow(DiagnosticEventDto e)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["kind"] = e.kind ?? "",
            ["tsUtc"] = e.tsUtc,
            ["testName"] = e.testName ?? "",
            ["storageType"] = e.storageType ?? ""
        };
        if (e.stage is not null) d["stage"] = e.stage;
        if (e.step is not null) d["step"] = e.step;
        if (e.elapsedMs is not null) d["elapsedMs"] = e.elapsedMs.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (e.data is not null)
        {
            foreach (var kv in e.data)
                d["data_" + kv.Key] = kv.Value;
        }

        return d;
    }

    public readonly struct StageScope : IDisposable
    {
        private readonly ManualTestTrace _trace;
        private readonly string _stage;
        private readonly Stopwatch _sw;

        internal StageScope(ManualTestTrace trace, string stage, Stopwatch sw)
        {
            _trace = trace;
            _stage = stage;
            _sw = sw;
        }

        public void Dispose()
        {
            _sw.Stop();
            _trace.EndStage(_stage, _sw.Elapsed.TotalMilliseconds);
        }
    }

    public readonly struct StepScope : IDisposable
    {
        private readonly ManualTestTrace _trace;
        private readonly string? _stage;
        private readonly string _step;
        private readonly Stopwatch _sw;

        internal StepScope(ManualTestTrace trace, string? stage, string step, Stopwatch sw)
        {
            _trace = trace;
            _stage = stage;
            _step = step;
            _sw = sw;
        }

        public void Dispose()
        {
            _sw.Stop();
            _trace.EndStep(_stage, _step, _sw.Elapsed.TotalMilliseconds);
        }
    }
}
