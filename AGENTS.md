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
| **Phase 1** | Done | Core engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width CHAR(n) only |
| **Phase 2** | Next | Indexes, PK/FK, constraints, relational metadata |
| **Phase 3** | Planned | VARCHAR, variable-width fields, storage evolution |

**Current focus:** Phase 2. Use [docs/plans/Phase2_Implementation_Plan.md](docs/plans/Phase2_Implementation_Plan.md) when available. When starting a new session, check [docs/plans/](docs/plans/) for the latest plan and phase status.

## Prompt Strategy

**Break work into small, testable chunks.** Do not ask for "build the whole database engine" in a single prompt.

### Bounded Task Examples

- "Implement the initial contract models for TableDefinition, ColumnDefinition, and CreateTableCommand."
- "Implement a tokenizer for the Phase 1 SQL subset."
- "Implement schema persistence: write and read schema.txt for a single table."

### Avoid

- "Build the complete SQL.txt engine."
- "Implement everything in Phase 1."

## Documentation

When adding features, update all relevant documentation. For each functionality, provide examples for **all implementation types** where the feature applies:

- **CLI (filesystem)** — `sqltxt exec --db ./Db "..."`
- **CLI (WASM)** — `sqltxt exec --db ./Db.wasmdb --wasm "..."`
- **Embedding (C#)** — `await engine.ExecuteAsync(...)`

See [docs/architecture/05-documentation-standards.md](docs/architecture/05-documentation-standards.md) and [.cursor/rules/sql-txt-documentation.mdc](.cursor/rules/sql-txt-documentation.mdc).

## Key References

- **Full specification:** [docs/specifications/01_Initial_Creation.md](docs/specifications/01_Initial_Creation.md)
- **Phase 1 prompts:** [docs/prompts/phase-1-cursor-prompts.md](docs/prompts/phase-1-cursor-prompts.md)
- **Architecture:** [docs/architecture/](docs/architecture/)
- **Storage format:** [docs/architecture/02-storage-format.md](docs/architecture/02-storage-format.md) — db/, Tables/, ~System/, sharding
- **Durability/sharding:** [docs/architecture/06-durability-and-sharding.md](docs/architecture/06-durability-and-sharding.md)
- **API/deployment:** [docs/architecture/07-api-and-deployment.md](docs/architecture/07-api-and-deployment.md) — CLI, Service, NuGet
- **Concurrency:** [docs/architecture/08-concurrency-and-locking.md](docs/architecture/08-concurrency-and-locking.md) — locking, NOLOCK
- **Design decisions (ADR-003):** [docs/decisions/adr-003-phase1-design-decisions.md](docs/decisions/adr-003-phase1-design-decisions.md)
- **Documentation standards:** [docs/architecture/05-documentation-standards.md](docs/architecture/05-documentation-standards.md)
- **WASM storage:** [docs/architecture/09-wasm-storage.md](docs/architecture/09-wasm-storage.md)
- **Sample Wiki database:** [docs/samples/wiki-database.md](docs/samples/wiki-database.md)
- **Plans:** [docs/plans/](docs/plans/)

## Project Structure

- `src/SqlTxt.Contracts` — Shared models and interfaces
- `src/SqlTxt.Core` — Domain primitives
- `src/SqlTxt.Storage` — On-disk persistence
- `src/SqlTxt.Parser` — SQL-like parsing
- `src/SqlTxt.Engine` — Execution layer
- `src/SqlTxt.Cli` — Command-line tool
- `src/SqlTxt.SampleApp` — Consumer example
- `tests/` — Unit and integration tests

## Cursor Rules

Project-specific rules are in `.cursor/rules/`. The `sql-txt-project-overview` rule applies to every session.
