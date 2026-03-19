# SQL.txt

A lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in **human-readable text files**.

SQL.txt is part learning tool, part experiment, and part a way to provide a simple but robust SQL experience. Yes, plenty of SQL tools already exist—but this one is different. It stores everything in plain text files you can open and inspect with any editor. There’s no server to run, no external dependencies to manage, and no black box. You get a real relational engine that you can understand, tweak, and learn from.

It may not be the right fit for high-throughput or commercial workloads where more specialized databases will outperform it. But for learning how databases work under the hood, exploring your data in a transparent way, or powering small projects that need a real RDBMS without the usual setup—SQL.txt aims to hit that sweet spot. It’s a cross-section of “teach me how this works” and “actually useful for real tasks.”

## Who is this for?

SQL.txt is for people who like to learn by doing, who enjoy poking around their data, and who get a kick out of trying something a little different. If you’ve ever wondered how a storage engine actually persists rows, how a parser turns SQL into executable steps, or how indexes and constraints fit together—and you’d rather see it in code than read about it in theory—this project is for you. It’s also for developers who want a minimal, embeddable datastore for prototypes, side projects, or tools where a full database server feels like overkill. Curiosity and pragmatism, in one package.

## Features

- **Storage backends (text | binary)** — Choose at database creation: `--storage:text` for human-readable learning/inspection, `--storage:binary` for performance
- **Human-readable on-disk storage** — Inspect and debug data with any text editor (text backend)
- **Lightweight local embedded usage** — No server, no external dependencies
- **WASM-compatible storage** — `--wasm` mode for browser-style storage; single `.wasmdb` file persistence
- **In-memory data management** — Index/metadata cache, optional file-level LRU cache, `--memory` load-into-RAM mode with optional `--persist`
- **Cross-platform** — Windows, macOS, Linux
- **Three build types** — CLI executable, NuGet API DLL, installable Service (Phase 2)
- **Async API** — All DB functions via async methods; supports concurrent API calls
- **Phase 2 relational** — Primary keys, foreign keys, UNIQUE, indexes, sharding, per-table/schema locks (ADR-009), rebalance
- **MVCC (pre-release)** — Row `xmin`/`xmax`, snapshot `SELECT`, `VacuumMvccRowsAsync`; see ADR-010 and [08-concurrency-and-locking](docs/architecture/08-concurrency-and-locking.md)
- **Phase 3 variable-width** — VARCHAR(n), length-prefixed row format, per-table format version
- **Structured progression** — Phased implementation from simple to relational engine
- **.NET 8** — Modern C# with async/await support

## Project Structure

```
SqlTxt.slnx
src/
  SqlTxt.Contracts/   # Shared models and interfaces
  SqlTxt.Core/        # Domain primitives and utilities
  SqlTxt.Storage/     # On-disk persistence
  SqlTxt.Parser/      # SQL-like command parsing
  SqlTxt.Engine/      # Execution layer
  SqlTxt.Cli/         # Command-line tool
  SqlTxt.SampleApp/   # Consumer example
tests/
  SqlTxt.*.Tests/     # Unit and integration tests
docs/
  architecture/      # System design
  specifications/    # Product specs
  prompts/           # Cursor implementation prompts
  decisions/         # ADRs
```

## Build & Test

```bash
dotnet build
dotnet test
```

### Manual tests (concurrency / sharding)

From the repo root, defaults use **`manual-test-artifacts/`** (gitignored except `README.md`):

```bash
dotnet run --project src/SqlTxt.ManualTests -- concurrency
dotnet run --project src/SqlTxt.ManualTests -- all --storage all
```

Omitting `--db` uses `manual-test-artifacts/run-<timestamp>/`. Omitting `--log` writes to `manual-test-artifacts/logs/`. By default, run artifacts are **deleted** after completion; add **`--save-db`** to keep databases. Override with e.g. `--db C:\temp\MyDb` when needed.

## Deployment

| Build | Description |
|-------|-------------|
| **CLI** | Standalone executable; `sqltxt create-db`, `sqltxt exec`, etc. |
| **CLI (WASM)** | Same CLI with `--wasm`; stores in `.wasmdb` file; future Blazor/browser target |
| **NuGet** | SqlTxt package; embed in APIs, websites |
| **Service** | Installable service (Phase 2) |

## Documentation

- [Getting Started](docs/getting-started.md) — Prerequisites, installation, choose your path
- [Getting Started (CLI)](docs/getting-started/cli.md) — CLI with filesystem storage
- [Getting Started (WASM)](docs/getting-started/wasm.md) — CLI with `--wasm` for browser-style storage
- [Getting Started (Embedding)](docs/getting-started/embedding.md) — Embed in C# applications
- [CLI Reference](docs/cli-reference.md) — Command-line usage
- [WASM Storage](docs/architecture/09-wasm-storage.md) — Browser-compatible storage design
- [Initial Creation Spec](docs/specifications/01_Initial_Creation.md) — Full specification
- [Post-Phase 3 Features](docs/specifications/02_Post_Phase3_Features.md) — Views, procedures, functions, and more
- [Architecture](docs/architecture/) — System design and storage format
- [Sample Wiki Database](docs/samples/wiki-database.md) — Example schema and scripts
- [Plans](docs/plans/) — Implementation plans
- [SQL:2023 Compliance Roadmap](docs/roadmap/00-sql2023-compliance-roadmap.md) — Full SQL:2023 feature mapping and deferred features
- [SQL:2023 Feature Registry](docs/roadmap/01-sql2023-feature-registry.md) — Complete feature list with phase assignments and spec references

## Roadmap

SQL.txt is built in phases toward **100% SQL:2023 compliance per phase**: start with a working engine and core CRUD, add relational features (indexes, keys, constraints), evolve storage for variable-width data, extend with query enrichment, schema evolution, and programmability. Each phase implements all applicable SQL:2023 features for its scope. See [SQL:2023 Compliance Roadmap](docs/roadmap/00-sql2023-compliance-roadmap.md) for execution order and [SQL:2023 Feature Registry](docs/roadmap/01-sql2023-feature-registry.md) for the complete feature list.

| Phase | Status | Scope |
|-------|--------|-------|
| **Stage 0** | Done | Solution scaffolding, design docs, Cursor guidance |
| **Phase 1** | Done | Core engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width CHAR(n) only |
| **Phase 2** | Done | Indexes, PK/FK, constraints, relational metadata |
| **Storage Abstraction** | Done | `--storage:text` \| `--storage:binary` at database creation |
| **Phase 3** | Done | VARCHAR, variable-width fields, storage evolution; SQL:2023 T055/T056/T062/T081 |
| **In-Memory Data Management** | Done | Index/metadata cache, CachingFileSystemAccessor (LRU), load-into-memory mode (`--memory` / `--persist`) |
| **Phase 3.5** | Next | Storage & ingest efficiency: true append I/O, batched multi-row INSERT (validation + index writes), sorted on-disk indexes per ADR-008; physical layout may use non-linear structures for speed |
| **Phase 4** | After 3.5 | JOINs, aggregates, ORDER BY, GROUP BY, subqueries; SQL:2023 |
| **CTE Phase** | Planned | WITH clause, recursive CTE; SQL:2023 |
| **Phase 5** | Planned | ALTER TABLE, transactions; SQL:2023 |
| **Phase 6** | Planned | Views, stored procedures, functions; SQL:2023 |
| **Phase 7** | Planned | Statistics (CREATE STATISTICS, histograms) |

## License

GNU General Public License v3.0 — See [LICENSE](LICENSE).
