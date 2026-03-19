# SqlTxt.ManualTests

Manual testing for SQL.txt: concurrency, sharding, Phase 4 query features, and performance.

## Purpose

Run manual tests from the terminal with configurable parameters. Output goes to the console and log files. Use this to validate concurrency behavior, sharding behavior, and **feature-level query scenarios** before or after changes.

## Prerequisites

- .NET 8 SDK
- SQL.txt Engine (referenced as project reference)
- For `--compare:localdb`: SQL Server LocalDB (default instance `(localdb)\MSSQLLocalDB`)

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
| `all` | Run all **legacy** tests (`concurrency` + `sharding` + `sharding-varchar`) |
| `phase4-bind-expr` | Phase 4.1: compound `WHERE` / expression binding ([Phase4_01](../../docs/plans/Phase4_01_Query_IR_and_Expressions_Plan.md)) |
| `phase4-joins` | Phase 4.2: INNER / LEFT JOIN ([Phase4_02](../../docs/plans/Phase4_02_Joins_Execution_Plan.md)) |
| `phase4-orderby` | Phase 4.3: `ORDER BY` ([Phase4_03](../../docs/plans/Phase4_03_OrderBy_Sort_Plan.md)) |
| `phase4-groupby` | Phase 4.4: `GROUP BY`, aggregates, `HAVING` ([Phase4_04](../../docs/plans/Phase4_04_GroupBy_Aggregates_Plan.md)) |
| `phase4-subqueries` | Phase 4.5: `IN`, `EXISTS`, scalar subqueries ([Phase4_05](../../docs/plans/Phase4_05_Subqueries_Decorrelation_Plan.md)) |
| `phase4-all` | Run all Phase 4 manual tests in order |

Until Phase 4 is implemented in the parser/engine, `phase4-*` tests **skip** (status `SKIPPED`, exit code 0). After implementation they must **pass** (real assertions).

## Common Options

| Option | Description | Default |
|--------|-------------|---------|
| `--db <path>` | Database parent path | `manual-test-artifacts/run-<timestamp>/` under repo when omitted |
| `--log <path>` | Log file path | `manual-test-artifacts/logs/ManualTests_<timestamp>.log` when omitted |
| `--storage <type>` | `text`, `binary`, or `all` | `text` |
| `--compare:<db>` | Comparison DB for timing (e.g. `localdb`) | Off |
| `--verbose` | Extra output | Off |

Use `--storage all` to run each test for both text and binary backends and log a comparison table of timing results.

### LocalDB comparison (`--compare:localdb`)

For **`concurrency`**, **`sharding`**, and **`sharding-varchar`**, you can add `--compare:localdb` to also run an equivalent scenario against SQL Server LocalDB. Results appear in the same summary table (e.g. `Sharding [localdb]`).

**Phase 4 tests (`phase4-*`):** `--compare:localdb` is **ignored**. Those scenarios are feature-specific to SQL.txt’s Phase 4 plan; LocalDB parity is not used, and LocalDB may not match the exact surface under test.

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
dotnet run --project src/SqlTxt.ManualTests -- all --db ./TestDb --storage all --compare:localdb --log ./results.log
dotnet run --project src/SqlTxt.ManualTests -- phase4-all --storage all
dotnet run --project src/SqlTxt.ManualTests -- phase4-joins --compare:localdb
```

The last command logs that LocalDB comparison is ignored for Phase 4 tests.

## Results Output

The final output is a summarized fixed-width text table, one line per test/storage pair. Skipped Phase 4 runs show `(skip)` in the test name column.

| Column | Description |
|--------|-------------|
| Test Run | Test name and storage type (e.g. `Sharding [text]`, `Phase4 joins [text] (skip)`) |
| Total (ms) | Wall-clock duration for that test |
| Avg/op (ms) | Average time per operation in a batch where applicable |
| Exec (ms) | Execution time (same as Total for each row) |

Use `--verbose` to also see detailed step timings and per-operation averages for each test.

## Log File

- Location: default under `manual-test-artifacts/logs/`, or `--log <path>`
- Format: Human-readable (not JSON) for quick inspection
- Contents: Timestamp, test name, parameters, results, step timings, averages, exceptions

## Extensibility

Add new test classes under `Tests/` and register them in [ManualTestProgram.cs](ManualTestProgram.cs). Pattern: implement a static `RunAsync` that returns `TestResult` and accepts `dbPath`, optional storage backend, and `ResultLogger`. For **major features**, add a dedicated manual test name and document it in the relevant `docs/plans/*.md` (see [AGENTS.md](../../AGENTS.md)).
