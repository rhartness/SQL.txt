# SqlTxt.ManualTests

Manual testing for SQL.txt: concurrency, sharding, and performance.

## Purpose

Run manual tests from the terminal with configurable parameters. Output goes to the console and log files. Use this to validate concurrency behavior, sharding behavior, and query performance before or after changes.

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
|------|--------------|
| `concurrency` | High concurrency: multi-thread INSERT, UPDATE, DELETE (and optional SELECT with NOLOCK) |
| `sharding` | Sharding: insert many Page rows (fixed-width), measure full scan and index lookup speed |
| `sharding-varchar` | Sharding: insert many Notes rows (VARCHAR), verify shard split and rebalance |
| `all` | Run all tests |

## Common Options

| Option | Description | Default |
|--------|-------------|---------|
| `--db <path>` | Database path | Current directory |
| `--log <path>` | Log file path | `ManualTests_<timestamp>.log` in current directory |
| `--storage <type>` | `text`, `binary`, or `all` | `text` |
| `--compare:<db>` | Run same test against comparison DB for timing comparison | Off |
| `--verbose` | Extra output | Off |

Use `--storage all` to run each test for both text and binary backends and log a comparison table of timing results.

Use `--compare:localdb` to also run the equivalent test against SQL Server LocalDB. Results appear in the same summary table (e.g., `Sharding [localdb]`). LocalDB uses the default instance; no connection config is required. Future comparison backends (e.g., SQL Server, PostgreSQL) will require connection config.

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

Sharding tests include: full scan, lookup by Id (PK), lookup by Slug (indexed), and group-by (CreatedById). Update these when features like JOINs or GROUP BY are added. The `sharding-varchar` test exercises VARCHAR tables, shard split with variable-width rows, and RebalanceTableAsync.

## Examples

```bash
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb --threads 4 --ops 20
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --shards 5 --rows 500
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --compare:localdb
dotnet run --project src/SqlTxt.ManualTests -- sharding-varchar --db ./TestDb --rows 200
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --storage all
dotnet run --project src/SqlTxt.ManualTests -- all --db ./TestDb --storage all --compare:localdb --log ./results.log
```

## Results Output

The final output is a summarized fixed-width text table, one line per test/storage pair:

```
=== Results Summary ===
-------------------------------------------------------------------------------
Test Run                     |     Total (ms) |    Avg/op (ms) |      Exec (ms)
-------------------------------------------------------------------------------
High Concurrency [text]      |         518.65 |          33.40 |         518.65
Sharding [text]              |         523.16 |           7.04 |         523.16
Sharding [binary]            |         312.48 |           8.13 |         312.48
-------------------------------------------------------------------------------
TOTAL                        |         726.55 |              - |         726.55
-------------------------------------------------------------------------------
```

| Column | Description |
|--------|-------------|
| Test Run | Test name and storage type (e.g., `Sharding [text]`) |
| Total (ms) | Wall-clock duration for that test |
| Avg/op (ms) | Average time per operation in a batch (e.g., avg INSERT ms) |
| Exec (ms) | Execution time (same as Total for each row) |

Use `--verbose` to also see detailed step timings and per-operation averages for each test.

## Log File

- Location: `ManualTests_<timestamp>.log` by default, or `--log <path>`
- Format: Human-readable (not JSON) for quick inspection
- Contents: Timestamp, test name, parameters, results, step timings, averages, exceptions

When `--storage all` is used, the summary table includes one row per test/storage pair (e.g., Sharding [text], Sharding [binary]) plus a TOTAL row.

## Extensibility

Add new test classes under `Tests/` and register them in `Program.cs`. Pattern: implement a static `RunAsync` that returns `TestResult` and accepts `dbPath`, options, and `ResultLogger`.
