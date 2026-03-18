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
| `--verbose` | Extra output | Off |

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

## Examples

```bash
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb
dotnet run --project src/SqlTxt.ManualTests -- concurrency --db ./TestDb --threads 4 --ops 20
dotnet run --project src/SqlTxt.ManualTests -- sharding --db ./TestDb --shards 5 --rows 500
dotnet run --project src/SqlTxt.ManualTests -- all --db ./TestDb --log ./results.log
```

## Log File

- Location: `ManualTests_<timestamp>.log` by default, or `--log <path>`
- Format: Human-readable (not JSON) for quick inspection
- Contents: Timestamp, test name, parameters, results, exceptions

## Extensibility

Add new test classes under `Tests/` and register them in `Program.cs`. Pattern: implement a static `RunAsync` that returns `TestResult` and accepts `dbPath`, options, and `ResultLogger`.
