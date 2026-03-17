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

| Phase | Scope |
|-------|-------|
| **Stage 0** | Solution scaffolding, design docs, Cursor guidance |
| **Phase 1** | Core engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width CHAR(n) only |
| **Phase 2** | Indexes, PK/FK, constraints, relational metadata |
| **Phase 3** | VARCHAR, variable-width fields, storage evolution |

## Build & Test

```bash
dotnet build
dotnet test
```

## Documentation

- [Initial Creation Spec](docs/specs/01_Initial_Creation.md) — Full specification
- [Architecture](docs/architecture/) — System design and storage format
- [Plans](docs/plans/) — Implementation plans

## License

GNU General Public License v3.0 — See [LICENSE](LICENSE).
