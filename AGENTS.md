# SQL.txt — Agent Instructions

## Project Context

**SQL.txt** is a lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in **human-readable text files**. It serves as both a learning tool and a practical embedded datastore.

## Goals

1. **Learning tool** — Readable, inspectable database engine for understanding storage engines, query parsing, CRUD semantics, metadata, schema evolution, and indexing.
2. **Practical embedded datastore** — Minimal dependency, NuGet-installable local database requiring only a package reference, writable directory, and optional CLI/consumer tooling.

## Phased Development Approach

| Stage/Phase | Status | Scope |
|-------------|--------|-------|
| **Stage 0** | Done | Solution scaffolding, design docs, Cursor guidance |
| **Phase 1** | Done | Core engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width CHAR(n) only; SQL:2023 subset |
| **Phase 2** | Done | Indexes, PK/FK, constraints, STOC, configurable sharding (20MB default), rebalance API |
| **Phase 3** | Done | VARCHAR, variable-width fields, storage evolution |
| **Phase 3.5** | Next | Storage & ingest efficiency (batched INSERT path, append I/O, sorted indexes); see [Phase3_5_Storage_Efficiency_Plan.md](docs/plans/Phase3_5_Storage_Efficiency_Plan.md) |
| **Phase 4** | After 3.5 | JOINs, aggregates, ORDER BY, GROUP BY, subqueries |
| **CTE Phase** | Planned | Common Table Expressions (WITH clause); non-recursive and recursive |
| **Phase 5** | Planned | ALTER TABLE, transactions |
| **Phase 6** | Planned | Views, stored procedures, functions |
| **Phase 7** | Planned | Statistics (CREATE STATISTICS, histograms, cardinality estimation) |

**Current focus:** Phase 3.5 (storage & ingest efficiency). When starting a new session, check [docs/plans/](docs/plans/) and [Phase3_5_Storage_Efficiency_Plan.md](docs/plans/Phase3_5_Storage_Efficiency_Plan.md).

## Prompt Strategy

**Break work into small, testable chunks.** Do not ask for "build the whole database engine" in a single prompt.

### Bounded Task Examples

- "Implement the initial contract models for TableDefinition, ColumnDefinition, and CreateTableCommand."
- "Implement a tokenizer for the Phase 1 SQL subset."
- "Implement schema persistence: write and read schema.txt for a single table."

### Avoid

- "Build the complete SQL.txt engine."
- "Implement everything in Phase 1."

When generating plan documents, consider asking: "Do you want specific manual tests generated for this feature?" Manual tests live in [src/SqlTxt.ManualTests](src/SqlTxt.ManualTests) and can be extended per feature.

### Manual Tests

- **Location:** [src/SqlTxt.ManualTests](src/SqlTxt.ManualTests)
- **When to run:** After storage, sharding, or concurrency changes
- **Tests:** `concurrency`, `sharding`, `sharding-varchar`, `all`
- **Defaults:** Use the repo’s **`manual-test-artifacts/`** tree — omit `--db` to get `manual-test-artifacts/run-<timestamp>/`; omit `--log` for `manual-test-artifacts/logs/ManualTests_<timestamp>.log`. Do **not** use `--db .` at the repo root for agent runs.
- **Retention:** Default **`--save-db` is off** — the run directory (or `WikiDb` / `VarcharShardingDb` / `ManualTest_*` under an explicit `--db`) is deleted after the run. Pass **`--save-db`** to keep databases for inspection.
- **Command:** `dotnet run --project src/SqlTxt.ManualTests -- <test> [--storage all] [--save-db]`
- **Prompt:** "Do you want specific manual tests generated for this feature?" when adding storage/sharding/concurrency features

## Efficiency Requirements (Knuth-Style)

Never implement the most straightforward approach for data-focused tasks. Think like a data or software scientist: structure all data management, building, linking, searching, and operations for maximum efficiency. For each new feature, consider **speed** (minimal I/O, buffering) and **memory** (streaming vs full load). For large data, prefer streaming, copy-on-write, and atomic renames. Breaking coding conventions is acceptable when it yields faster, more efficient, or more durable code.

- **Speed:** Minimize I/O; use streaming over full-file loads when files may grow large.
- **Memory:** Prefer O(1) or O(row) memory; avoid loading entire tables when streaming is possible.
- **Durability:** Use atomic writes (temp file + rename); no partial writes.

See [docs/architecture/10-performance-and-efficiency.md](docs/architecture/10-performance-and-efficiency.md) and [docs/plans/Efficiency_Audit_Methodology.md](docs/plans/Efficiency_Audit_Methodology.md).

## Documentation

When adding features, update all relevant documentation. For each functionality, provide examples for **all implementation types** where the feature applies:

- **CLI (filesystem)** — `sqltxt exec --db ./Db "..."`
- **CLI (WASM)** — `sqltxt exec --db ./Db.wasmdb --wasm "..."`
- **Embedding (C#)** — `await engine.ExecuteAsync(...)`

See [docs/architecture/05-documentation-standards.md](docs/architecture/05-documentation-standards.md) and [.cursor/rules/sql-txt-documentation.mdc](.cursor/rules/sql-txt-documentation.mdc).

## Plan Execution Rules (Mandatory)

These rules apply to **every** sub-plan execution:

1. **On plan completion:** Update [README.md](README.md) Roadmap table (phase status, scope changes)
2. **On feature addition:** Update [docs/architecture/11-sql2023-mapping.md](docs/architecture/11-sql2023-mapping.md) and [docs/roadmap/01-sql2023-feature-registry.md](docs/roadmap/01-sql2023-feature-registry.md) with implemented feature IDs
3. **On API/CLI change:** Update [docs/cli-reference.md](docs/cli-reference.md), Getting Started docs, and XML comments
4. **On storage change:** Update [docs/architecture/02-storage-format.md](docs/architecture/02-storage-format.md)
5. **On storage/format change:** Run manual tests (`sharding`, `sharding-varchar`, `concurrency`) and extend them if the change affects row format, sharding, or locking
6. **Examples:** Provide examples for CLI (filesystem), CLI (WASM), and Embedding per [docs/architecture/05-documentation-standards.md](docs/architecture/05-documentation-standards.md)

## Key References

- **Full specification:** [docs/specifications/01_Initial_Creation.md](docs/specifications/01_Initial_Creation.md)
- **Post-Phase 3 features:** [docs/specifications/02_Post_Phase3_Features.md](docs/specifications/02_Post_Phase3_Features.md)
- **Phase 1 prompts:** [docs/prompts/phase-1-cursor-prompts.md](docs/prompts/phase-1-cursor-prompts.md)
- **Architecture:** [docs/architecture/](docs/architecture/)
- **Storage format:** [docs/architecture/02-storage-format.md](docs/architecture/02-storage-format.md) — db/, Tables/, ~System/, sharding, STOC
- **SQL:2023 mapping:** [docs/architecture/11-sql2023-mapping.md](docs/architecture/11-sql2023-mapping.md)
- **SQL:2023 feature registry:** [docs/roadmap/01-sql2023-feature-registry.md](docs/roadmap/01-sql2023-feature-registry.md) — Full feature list with phase assignments
- **Statistics design:** [docs/decisions/adr-006-statistics-design.md](docs/decisions/adr-006-statistics-design.md)
- **Durability/sharding:** [docs/architecture/06-durability-and-sharding.md](docs/architecture/06-durability-and-sharding.md)
- **API/deployment:** [docs/architecture/07-api-and-deployment.md](docs/architecture/07-api-and-deployment.md) — CLI, Service, NuGet
- **Concurrency:** [docs/architecture/08-concurrency-and-locking.md](docs/architecture/08-concurrency-and-locking.md) — locking, NOLOCK
- **Design decisions (ADR-003):** [docs/decisions/adr-003-phase1-design-decisions.md](docs/decisions/adr-003-phase1-design-decisions.md)
- **Documentation standards:** [docs/architecture/05-documentation-standards.md](docs/architecture/05-documentation-standards.md)
- **WASM storage:** [docs/architecture/09-wasm-storage.md](docs/architecture/09-wasm-storage.md)
- **Performance/efficiency:** [docs/architecture/10-performance-and-efficiency.md](docs/architecture/10-performance-and-efficiency.md)
- **Efficiency audit:** [docs/plans/Efficiency_Audit_Methodology.md](docs/plans/Efficiency_Audit_Methodology.md)
- **Sample Wiki database:** [docs/samples/wiki-database.md](docs/samples/wiki-database.md)
- **Plans:** [docs/plans/](docs/plans/)

## Project Structure

- `src/SqlTxt.Contracts` — Shared models and interfaces
- `src/SqlTxt.Core` — Domain primitives
- `src/SqlTxt.Storage` — On-disk persistence
- `src/SqlTxt.Parser` — SQL-like parsing
- `src/SqlTxt.Engine` — Execution layer
- `src/SqlTxt.Cli` — Command-line tool
- `src/SqlTxt.ManualTests` — Manual tests (concurrency, sharding, performance)
- `src/SqlTxt.SampleApp` — Consumer example
- `tests/` — Unit and integration tests

## Cursor Rules

Project-specific rules are in `.cursor/rules/`. The `sql-txt-project-overview` rule applies to every session.
