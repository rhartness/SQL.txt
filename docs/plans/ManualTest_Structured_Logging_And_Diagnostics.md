# Manual tests — structured logging and diagnostics (Sub-plan 1)

## Purpose

Define **opt-in**, **structured** observability for manual and diagnostic runs: **stages** and **steps** with timing and context, **iteration-level error counts**, and **failure bundles** rich enough for humans and for **Cursor/agents** to diagnose issues (e.g. Sharding, VARCHAR rebalance) without re-running blindly.

This sub-plan is **implementation-focused** for contracts and harness code; integration with [`ManualTestProgram`](../../src/SqlTxt.ManualTests/ManualTestProgram.cs) and Markdown/JSONL artifacts is specified in [ManualTest_Harness_Reporting_Integration.md](ManualTest_Harness_Reporting_Integration.md). Agent-facing workflow is in [ManualTest_Docs_And_Agent_Rules.md](ManualTest_Docs_And_Agent_Rules.md).

## Goals

- Single **trace abstraction** used by **all** current and future manual tests (`Tests/`, `Compare/`).
- **Stage** / **step** vocabulary with stable string ids (grep- and merge-friendly).
- **Iterative** tests record **success/error counts** and optional capped **per-iteration error samples**.
- On failure, produce a **FailureBundle** (JSON-serializable DTO) with paths, counts, last query context, shard/index hints, and length-capped SQL redaction rules.
- **Optional** Engine/Storage hooks (`IDiagnosticLogger` or equivalent) behind a **no-op default** so NuGet embedders pay zero cost unless they inject diagnostics.

## Non-goals

- Replacing production logging for SqlTxt.Service or hosted deployments (separate ADR if needed).
- OpenTelemetry / distributed tracing (future extension only).
- Changing default verbosity of embedded `DatabaseEngine` for normal consumers.

## Architecture

### Layering

| Layer | Responsibility |
|--------|----------------|
| **Contracts** (or `SqlTxt.Diagnostics` if NuGet surface must stay pristine) | DTOs: `DiagnosticEvent`, `FailureBundle`, `StageBoundary`, enums for severity. No file I/O. |
| **ManualTests** | `ManualTestTrace`, `ITestDiagnosticSink`, ring buffer of recent events, `BuildFailureBundle()`, integration with [`ResultLogger`](../../src/SqlTxt.ManualTests/Results/ResultLogger.cs). |
| **Engine/Storage (phase 2)** | Optional `IDiagnosticLogger` registered on `DatabaseEngine`; emit high-value events only when non-null / enabled. |

**Package placement decision (record in PR):**

- **Option A:** Types live in `SqlTxt.Contracts` under `SqlTxt.Diagnostics` namespace (versioned with engine; consumers may reference for custom sinks).
- **Option B:** Types live only in `SqlTxt.ManualTests` until a second consumer exists; promote to Contracts later.

Default recommendation: **Option B for MVP**, promote when CLI or Engine needs the same DTOs on the wire.

### Event model (sketch)

- `RunId` — GUID or correlation id shared across main log, `.errors-and-comparison.md`, `.diagnostics.jsonl` (see Sub-plan 3).
- `TimestampUtc`, `TestName`, `StorageType` (optional).
- `Stage` — coarse: `Setup`, `Execute`, `Assert`, `Teardown`.
- `Step` — fine: `CreateDatabase`, `InsertBatch`, `Rebalance`, `QueryFullScan`, `QueryPkLookup`, `LocalDbConnect`, etc.
- `ElapsedMs` (on step end).
- `Counters` — optional key-value (e.g. `rowsInserted`, `shardFiles`, `iterationErrors`).
- `Context` — flat dictionary string→string (paths, row counts); **size-limited** per event.

### ManualTestTrace API (sketch)

```csharp
// Pseudocode — align naming with repo conventions when implementing
using var stage = trace.BeginStage("Assert", "RowCountAfterRebalance");
using var step = trace.BeginStep("QueryFullScan", new() { ["table"] = "Notes" });
// ...
trace.RecordIterationOutcome(ok: false, detail: "timeout on thread 3"); // capped retention
trace.IncrementCounter("engineErrors", 1);
```

### FailureBundle (sketch JSON)

```json
{
  "runId": "...",
  "testName": "Sharding (VARCHAR)",
  "storageType": "binary",
  "failedStage": "Assert",
  "failedStep": "QueryPkLookup",
  "message": "PK lookup Id=100 expected 1 row, got 0",
  "paths": {
    "databaseRoot": "...",
    "tableFolder": ".../Tables/Notes"
  },
  "lastStructuredEvents": [  ],
  "artifactHints": {
    "shardCount": 4,
    "rowCountExpected": 200,
    "rowCountObserved": 0
  },
  "sqlSnippetRedacted": null
}
```

**Redaction / limits**

- Max SQL snippet length (e.g. 2–4 KB); truncate with hash of full text for dedup.
- Strip connection strings; avoid secrets in `Context`.

### Pilot adoption

1. Implement trace + sink in ManualTests only.
2. Pilot **`VarcharShardingTest`** and **`ShardingTest`** (highest diagnostic payoff).
3. Roll through **`HighConcurrencyTest`**, Phase 4 manual tests, **`LocalDbComparisons`** (stage per SQL batch).

### Engine/Storage phase 2 (optional)

- Interface: `IDiagnosticLogger` with `LogCommandStart/End`, `LogIndexLookup`, `LogRebalancePhase`, etc.
- `DatabaseEngine` accepts optional `IDiagnosticLogger?` in constructor or options bag; **default null**.
- Events must be **cheap** when disabled (single null check).

## File touch list (expected)

| Area | Files |
|------|--------|
| New | `src/SqlTxt.ManualTests/Diagnostics/ManualTestTrace.cs` (or `SqlTxt/Trace/`), `FailureBundle.cs`, `ITestDiagnosticSink.cs` |
| Tests | All under `src/SqlTxt.ManualTests/Tests/*.cs`, `Compare/*.cs` — incremental |
| Engine (later) | `DatabaseEngine.cs`, storage entry points — optional |

## Rollout order

1. DTOs + no-op sink + file/jsonl sink (see Sub-plan 3).
2. Pilot sharding tests + document bundle shape.
3. Remaining manual tests.
4. Optional Engine hooks after bundle format stabilizes.

## Definition of done (checklist)

- [x] `ManualTestTrace` (or equivalent) implemented with stage/step scope and counter API.
- [x] FailureBundle builder invoked from pilot and rolled-out manual tests on failure paths.
- [x] Iteration helpers documented; cap on stored per-iteration errors enforced.
- [x] Sub-plan 3 references stable **RunId** and filename conventions for `.diagnostics.jsonl`.
- [ ] **Phase D (deferred):** Optional Engine `IDiagnosticLogger` — **not implemented**; embeddable `DatabaseEngine` has no execution metrics hook yet. Revisit after bundle/trace format is stable in the field (see § “Engine/Storage phase 2” above). Consumers should use harness-only diagnostics until then.

## Stable stage/step ids

See [src/SqlTxt.ManualTests/README.md](../../src/SqlTxt.ManualTests/README.md) § “Stable stage and step ids” for the curated table used in sharding, concurrency, Phase 4, and LocalDB parity runners.

## References

- [ManualTest_Harness_Reporting_Integration.md](ManualTest_Harness_Reporting_Integration.md)
- [ManualTest_Docs_And_Agent_Rules.md](ManualTest_Docs_And_Agent_Rules.md)
- [AGENTS.md](../../AGENTS.md) — manual test defaults
- [ManualTestIssuesReport.cs](../../src/SqlTxt.ManualTests/Results/ManualTestIssuesReport.cs) — current comparison Markdown
