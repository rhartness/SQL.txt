# SQL.txt

A lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in **human-readable text files**.

## Goals

1. **Learning tool** — A readable, inspectable database engine for understanding storage engines, query parsing, CRUD semantics, metadata, schema evolution, and indexing.

2. **Practical embedded datastore** — A minimal dependency, NuGet-installable local database requiring only:
   - A package reference
   - A writable directory
   - Optional CLI / consumer tooling

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

## Development Phases

| Phase | Status | Scope |
|-------|--------|-------|
| **Stage 0** | Done | Solution scaffolding, design docs, Cursor guidance |
| **Phase 1** | Done | Core engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width CHAR(n) only |
| **Phase 2** | Next | Indexes, PK/FK, constraints, relational metadata |
| **Phase 3** | Planned | VARCHAR, variable-width fields, storage evolution |

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
- [Architecture](docs/architecture/) — System design and storage format
- [Sample Wiki Database](docs/samples/wiki-database.md) — Example schema and scripts
- [Plans](docs/plans/) — Implementation plans

## License

GNU General Public License v3.0 — See [LICENSE](LICENSE).
