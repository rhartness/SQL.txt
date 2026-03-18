# SQL.txt

A lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in **human-readable text files**.

SQL.txt is part learning tool, part experiment, and part a way to provide a simple but robust SQL experience. Yes, plenty of SQL tools already exist—but this one is different. It stores everything in plain text files you can open and inspect with any editor. There’s no server to run, no external dependencies to manage, and no black box. You get a real relational engine that you can understand, tweak, and learn from.

It may not be the right fit for high-throughput or commercial workloads where more specialized databases will outperform it. But for learning how databases work under the hood, exploring your data in a transparent way, or powering small projects that need a real RDBMS without the usual setup—SQL.txt aims to hit that sweet spot. It’s a cross-section of “teach me how this works” and “actually useful for real tasks.”

## Who is this for?

SQL.txt is for people who like to learn by doing, who enjoy poking around their data, and who get a kick out of trying something a little different. If you’ve ever wondered how a storage engine actually persists rows, how a parser turns SQL into executable steps, or how indexes and constraints fit together—and you’d rather see it in code than read about it in theory—this project is for you. It’s also for developers who want a minimal, embeddable datastore for prototypes, side projects, or tools where a full database server feels like overkill. Curiosity and pragmatism, in one package.

## Features

- **Human-readable on-disk storage** — Inspect and debug data with any text editor
- **Lightweight local embedded usage** — No server, no external dependencies
- **WASM-compatible storage** — `--wasm` mode for browser-style storage; single `.wasmdb` file persistence
- **Cross-platform** — Windows, macOS, Linux
- **Three build types** — CLI executable, NuGet API DLL, installable Service (Phase 2)
- **Async API** — All DB functions via async methods; supports concurrent API calls
- **Structured progression** — Phased implementation from simple to relational engine
- **.NET 8** — Modern C# with async/await support

## Project Structure

```
SqlTxt.sln
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

## Roadmap

SQL.txt is built in phases: start with a working engine and core CRUD, then add relational features (indexes, keys, constraints), evolve the storage format for variable-width data, and extend with query enrichment, schema evolution, and programmability (views, procedures, functions). The focus is on incremental, testable progress rather than big-bang releases.

| Phase | Status | Scope |
|-------|--------|-------|
| **Stage 0** | Done | Solution scaffolding, design docs, Cursor guidance |
| **Phase 1** | Done | Core engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width CHAR(n) only |
| **Phase 2** | Done | Indexes, PK/FK, constraints, relational metadata |
| **Phase 3** | Next | VARCHAR, variable-width fields, storage evolution |
| **Phase 4** | Planned | JOINs, aggregates, ORDER BY, GROUP BY, subqueries |
| **Phase 5** | Planned | ALTER TABLE, transactions |
| **Phase 6** | Planned | Views, stored procedures, functions |

## License

GNU General Public License v3.0 — See [LICENSE](LICENSE).
