# SqlTxt.ManualTests

Manual testing for SQL.txt: concurrency, sharding, Phase 4 query features, and performance.

## Purpose

Run manual tests from the terminal with configurable parameters. Output goes to the console and log files. Use this to validate concurrency behavior, sharding behavior, and **feature-level query scenarios** before or after changes.

## Prerequisites

- .NET 8 SDK
- SQL.txt Engine (referenced as project reference)
- For **LocalDB** runs (included automatically with `--storage all`, or via `--compare:localdb`): SQL Server LocalDB (default instance `(localdb)\MSSQLLocalDB`)

## How to Run

```bash
dotnet run --project src/SqlTxt.ManualTests -- <test> [options]
```

## Tests

| Test | Description |
|------|-------------|
| `concurrency` | High concurrency: multi-thread INSERT, UPDATE, DELETE (and optional SELECT with NOLOCK) |
| `sharding` | Sharding: insert many Page rows (fixed-width), measure full scan and index lookup speed |
| `sharding-varchar` | Sharding: insert many Notes rows (VARCHAR), verify shard split and rebalance |
| **`all`** | **Full suite:** `concurrency`, `sharding`, `sharding-varchar`, then every Phase 4 subtest (`phase4-bind-expr` … `phase4-subqueries`), in that order |
| `phase4-bind-expr` | Phase 4.1: compound `WHERE` / expression binding ([Phase4_01](../../docs/plans/Phase4_01_Query_IR_and_Expressions_Plan.md)) |
| `phase4-joins` | Phase 4.2: INNER / LEFT JOIN ([Phase4_02](../../docs/plans/Phase4_02_Joins_Execution_Plan.md)) |
| `phase4-orderby` | Phase 4.3: `ORDER BY` ([Phase4_03](../../docs/plans/Phase4_03_OrderBy_Sort_Plan.md)) |
| `phase4-groupby` | Phase 4.4: `GROUP BY`, aggregates, `HAVING` ([Phase4_04](../../docs/plans/Phase4_04_GroupBy_Aggregates_Plan.md)) |
| `phase4-subqueries` | Phase 4.5: `IN`, `EXISTS`, scalar subqueries ([Phase4_05](../../docs/plans/Phase4_05_Subqueries_Decorrelation_Plan.md)) |
| `phase4-all` | Run all Phase 4 manual tests in order (same subtests as the Phase 4 block inside `all`) |

Per-phase bundles (`phase4-all`, and future `phaseN-all`) stay available for running only that phase.

### `--storage all` (required behavior)

When you pass **`--storage all`**, the driver runs, **for each applicable manual scenario**:

1. **SQL.txt** with **text** filesystem backend (under `ManualTest_Text/` when using the default layout for `all` / multi-backend runs).
2. **SQL.txt** with **binary** filesystem backend (under `ManualTest_Binary/`).
3. **LocalDB**, for every test that has a LocalDB equivalent implemented in [Compare/LocalDbComparisons.cs](Compare/LocalDbComparisons.cs) (including [Compare/LocalDbComparisons.Phase4.cs](Compare/LocalDbComparisons.Phase4.cs) for Phase 4).

You do **not** need `--compare:localdb` to get LocalDB when using `--storage all`. If you pass both, LocalDB is **not** executed twice.

### When LocalDB is skipped

Only skip LocalDB for a scenario when SQL.txt implements something **that has no meaningful T-SQL / SQL Server equivalent** in the manual test harness. In that case, implement a **skipped** `TestResult` with a clear reason (or omit the LocalDB runner until parity exists). Document the gap in the plan or this README.

## Common Options

| Option | Description | Default |
|--------|-------------|---------|
| `--db <path>` | Database parent path | `manual-test-artifacts/run-<timestamp>/` under repo when omitted |
| `--log <path>` | Log file path | `manual-test-artifacts/logs/ManualTests_<timestamp>.log` when omitted |
| `--storage <type>` | `text`, `binary`, or `all` | `text` |
| `--compare:<db>` | With `text` or `binary` only: also run LocalDB (`localdb`). Redundant if `--storage all`. | Off |
| `--save-db` | Keep DB folders after the run | Off |
| `--verbose` | Extra output | Off |
| `--require-beat-localdb` | Exit 1 if any passing SqlTxt text/binary run is slower than LocalDB for the same test | Off |
| `--diagnostics` | Write structured JSON Lines trace: `<log_basename>.diagnostics.jsonl` next to the main log; `RunId` in log header and on each `TestResult` | Off |
| `--fail-on-deficit-ratio <r>` | Exit 1 if any duration ratio in `#deficits` exceeds *r* (see secondary `.md`); *r* &gt; 0 | Off |

## Diagnostics and failure artifacts

- **RunId** — printed at the start of the run and embedded in `TestResult` rows after the summary step; ties together the `.log`, `.diagnostics.jsonl`, `.errors-and-comparison.md`, and `.failure-bundle.json` (same base name as the log file under `--log`).
- **`--diagnostics`** — optional JSONL stream of `runStart`, `stageStart` / `stageEnd`, `stepStart` / `stepEnd`, `counter`, and `iterationError` events (see [ManualTest_Structured_Logging_And_Diagnostics.md](../../docs/plans/ManualTest_Structured_Logging_And_Diagnostics.md)).
- **`.failure-bundle.json`** — written when a test fails (assertion list or exception); includes `failedStage` / `failedStep`, paths, optional SQL snippet, and recent ring-buffer events when diagnostics were on.

### Stable stage and step ids (grep-friendly)

Use these strings when searching JSONL or bundles:

| Area | Stages | Steps (examples) |
|------|--------|-------------------|
| Sharding (fixed) | `Setup`, `Insert`, `Query` | `CreateDatabase`, `CreateUserTable`, `CreatePageTable`, `CreateIndexes`, `SeedUser`, `BatchInsert`, `QueryFullScan`, `QueryPkLookup`, `QueryBySlugIndex`, `QueryByCreatedByIdIndex` |
| Sharding (VARCHAR) | `Setup`, `Insert`, `AssertPreRebalance`, `Rebalance`, `AssertPostRebalance` | `CreateDatabase`, `CreateNotesTable`, `BatchInsert`, `QueryFullScanPreRebalance`, `QueryPkLookupPreRebalance`, `RebalanceTableAsync`, `QueryFullScanAfterRebalance` |
| High concurrency (SqlTxt) | `Setup`, `ConcurrentDml`, (`Teardown` n/a) | `BuildSampleWiki`, `AwaitWorkers` |
| High concurrency (LocalDB) | `Setup`, `ConcurrentDml`, `Teardown` | `LocalDbCreateAndSchema`, `AwaitWorkers`, `DropDatabase` |
| LocalDB sharding parity | `Setup`, `Teardown` + nested `Query` | `CreateDatabaseAndSchema`, `BatchInsert`, `QueryFullScan`, `QueryPkLookup`, … |
| Phase 4 (SqlTxt) | `Setup`, `Query` | Schema/seed inside `Setup`; assertion steps vary (`ValidateCompoundWhere`, `InnerJoin`, …) |
| LocalDB Phase 4 | `LocalDbPhase4` | `SetupAndQuery` |

## Concurrency Options

| Option | Description | Default |
|--------|-------------|---------|
| `--threads <n>` | Writer threads | 8 |
| `--ops <n>` | Operations per thread | 50 |
| `--readers <n>` | Concurrent SELECT threads with NOLOCK | 0 |

## Sharding Options

| Option | Description | Default |
|--------|-------------|---------|
| `--shards <n>` | Desired shard count for Page/Notes table | 5 |
| `--rows <n>` | Number of Page/Notes rows to insert | 500 |

Sharding tests include: full scan, lookup by Id (PK), lookup by Slug (indexed), and filter by `CreatedById`. Update these when features like JOINs or true `GROUP BY` are added. The `sharding-varchar` test exercises VARCHAR tables, shard split with variable-width rows, and `RebalanceTableAsync`.

## Examples

```bash
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb --threads 4 --ops 20
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --shards 5 --rows 500
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --compare:localdb
dotnet run --project src/SqlTxt.ManualTests -- sharding-varchar --db ./TestDb --rows 200
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --storage all
dotnet run --project src/SqlTxt.ManualTests -- all --storage all
dotnet run --project src/SqlTxt.ManualTests -- phase4-all --storage all
```

## SqlTxt vs LocalDB interpretation

- **High concurrency:** The harness checks **correctness/behavior** against LocalDB, not a throughput SLA. SqlTxt is file-backed with locking; LocalDB is not. Large duration ratios in the secondary report (`#deficits`) for concurrency are **informational** unless you opt into `--require-beat-localdb` or `--fail-on-deficit-ratio`.
- **`phase4-all`, full `all` suite, + LocalDB:** When all five Phase 4 subtests run as a consecutive block with LocalDB enabled, LocalDB uses **one** temporary database (amortized CREATE/DROP). Each scenario’s `TestResult` duration covers that scenario’s DDL and queries only; shared provisioning time appears in details as **`LocalDbProvisionMs`** when **`Phase4LocalDbSingleDatabase`** is true. A single Phase 4 subtest still provisions its own database.

## Results Output

The final output is a pivot-style summary: **one row per test**, with **two header rows**—storage names (`text`, `binary`, `localdb`, …) on the first row, then **Total**, **Avg**, and **Ops** under each storage. Data cells use the same three metrics (`—` when not applicable); `SKIP` and `FAIL` behave as before. **Avg** prefers explicit `Avg_*_Ms` details, then the first `Step_*_Ms` / `Step_*_Count` pair, then total ms ÷ ops when ops can be inferred (`OperationsCount` or sum of `Step_*_Count`). **Ops** uses `OperationsCount` when greater than zero, otherwise the sum of `Step_*_Count` values. When **two or more passing** storage runs exist for a row, the **fastest** one(s) by total ms show **parentheses around the Total (ms)** value only (ties each get parens). The **TOTAL ms** footer uses the same rule on summed totals per storage column. If no `StorageType` is present, the harness falls back to a simple list.

**Secondary report:** next to the main log, a Markdown file `ManualTests_<timestamp>.errors-and-comparison.md` lists **failures** (anchor `#failures`), a **SqlTxt vs LocalDB** table (`#vs-localdb`), and a **slower-than-LocalDB** list (`#slower-than-localdb`) for comparable passing runs. The primary log ends with the path to this file.

**Strict performance gate:** pass `--require-beat-localdb` to fail the process (exit code 1) when any passing SqlTxt **text** or **binary** run is strictly slower than **LocalDB** for the same test name (after functional passes). Use this in CI when SqlTxt must outperform LocalDB, not only match correctness.

Use `--verbose` to also see detailed step timings and per-operation averages for each test.

## Log File

- Location: default under `manual-test-artifacts/logs/`, or `--log <path>`
- Format: Human-readable (not JSON) for quick inspection
- Contents: Timestamp, test name, parameters, results, step timings, averages, exceptions
- Companion: `<same_base>.errors-and-comparison.md` — failures + SqlTxt vs LocalDB (linkable `#failures`, `#vs-localdb`, `#slower-than-localdb`)

## Extensibility

1. Add test classes under `Tests/` (static `RunAsync` → `TestResult`; `dbPath`, optional SqlTxt storage backend, `ResultLogger`).
2. Register the test id in [ManualTestProgram.cs](ManualTestProgram.cs):
   - Append to **`LegacyOrderedSubtests`** and/or **`Phase4OrderedSubtests`** (or a future phase array), and add **`FullSuiteOrderedSubtests`** linkage so `all` includes it.
3. If the scenario can be mirrored in SQL Server, add **`LocalDbComparisons`** (or a partial file) and invoke it from **`RunLegacySubtestExpandedAsync`** / **`RunPhase4OneAsync`** (or the phase-appropriate dispatcher) when `storage == "localdb"`.
4. If LocalDB cannot represent the feature, return a **skipped** result with a documented reason instead of a failing assert.
5. Document the test in the relevant `docs/plans/*.md` (see [AGENTS.md](../../AGENTS.md)).
