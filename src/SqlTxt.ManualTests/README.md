# SqlTxt.ManualTests

Manual testing for SQL.txt: concurrency, sharding, and performance.

## Purpose

Run manual tests from the terminal with configurable parameters. Output goes to the console and log files. Use this to validate concurrency behavior, sharding behavior, and query performance before or after changes.

## Prerequisites

- .NET 8 SDK
- SQL.txt Engine (referenced as project reference)

## How to Run

```bash
dotnet run --project src/SqlTxt.ManualTests -- <test> [options]
```

## Tests

| Test | Description |
|------|--------------|
| `concurrency` | High concurrency: multi-thread INSERT, UPDATE, DELETE (and optional SELECT with NOLOCK) |
| `sharding` | Sharding: insert many Page rows, measure full scan and index lookup speed |
| `all` | Run both tests |

## Common Options

| Option | Description | Default |
|--------|-------------|---------|
| `--db <path>` | Database path | Current directory |
| `--log <path>` | Log file path | `ManualTests_<timestamp>.log` in current directory |
| `--storage <type>` | `text`, `binary`, or `all` | `text` |
| `--verbose` | Extra output | Off |

Use `--storage all` to run each test for both text and binary backends and log a comparison table of timing results.

## Concurrency Options

| Option | Description | Default |
|--------|-------------|---------|
| `--threads <n>` | Writer threads | 8 |
| `--ops <n>` | Operations per thread | 50 |
| `--readers <n>` | Concurrent SELECT threads with NOLOCK | 0 |

## Sharding Options

| Option | Description | Default |
|--------|-------------|---------|
| `--shards <n>` | Desired shard count for Page table | 5 |
| `--rows <n>` | Number of Page rows to insert | 500 |

Sharding tests include: full scan, lookup by Id (PK), lookup by Slug (indexed), and group-by (CreatedById). Update these when features like JOINs or GROUP BY are added.

## Examples

```bash
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb --threads 4 --ops 20
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --shards 5 --rows 500
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --storage all
dotnet run --project src/SqlTxt.ManualTests -- all --db ./TestDb --storage all --log ./results.log
```

## Log File

- Location: `ManualTests_<timestamp>.log` by default, or `--log <path>`
- Format: Human-readable (not JSON) for quick inspection
- Contents: Timestamp, test name, parameters, results, exceptions

When `--storage all` is used, a comparison table is written first:

```
=== Results Comparison (text vs binary) ===
Test                     Storage    Status   Duration(ms)       Ops    Success       Fail
----------------------------------------------------------------------------------------
High Concurrency         text       PASS        1234.56          400        400        0
High Concurrency         binary     PASS        1098.12          400        400        0
Sharding                 text       PASS         567.89          504        504        0
Sharding                 binary     PASS         432.10          504        504        0
```

## Extensibility

Add new test classes under `Tests/` and register them in `Program.cs`. Pattern: implement a static `RunAsync` that returns `TestResult` and accepts `dbPath`, options, and `ResultLogger`.
