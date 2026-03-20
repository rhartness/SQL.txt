namespace SqlTxt.ManualTests.Diagnostics;

/// <summary>
/// Safe stage/step scopes when <see cref="ManualTestRunContext.CurrentOrFallback"/> may be null (diagnostics off).
/// </summary>
public static class ManualTestTraceScope
{
    public static StageHandle Stage(ManualTestTrace? trace, string stageName) => new(trace, stageName);

    public static StepHandle Step(ManualTestTrace? trace, string stepName, IReadOnlyDictionary<string, string>? context = null) =>
        new(trace, stepName, context);

    public struct StageHandle : IDisposable
    {
        private readonly bool _active;
        private ManualTestTrace.StageScope _inner;

        internal StageHandle(ManualTestTrace? trace, string stageName)
        {
            if (trace is null)
            {
                _active = false;
                _inner = default;
                return;
            }

            _active = true;
            _inner = trace.BeginStage(stageName);
        }

        public void Dispose()
        {
            if (_active)
                _inner.Dispose();
        }
    }

    public struct StepHandle : IDisposable
    {
        private readonly bool _active;
        private ManualTestTrace.StepScope _inner;

        internal StepHandle(ManualTestTrace? trace, string stepName, IReadOnlyDictionary<string, string>? context)
        {
            if (trace is null)
            {
                _active = false;
                _inner = default;
                return;
            }

            _active = true;
            _inner = trace.BeginStep(stepName, context);
        }

        public void Dispose()
        {
            if (_active)
                _inner.Dispose();
        }
    }
}
