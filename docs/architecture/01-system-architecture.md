# System Architecture

## Overview

SQL.txt uses a layered architecture with clear separation between parsing, execution, storage, and file system access. The engine is **cross-platform** (Windows, macOS, Linux).

## Project Dependencies

```
SqlTxt.Contracts (no deps)
    ↑
SqlTxt.Core (Contracts)
    ↑
SqlTxt.Storage (Contracts, Core)    SqlTxt.Parser (Contracts, Core)
    ↑                                       ↑
    └───────────────┬───────────────────────┘
                    ↑
            SqlTxt.Engine (Contracts, Core, Storage, Parser)
                    ↑
    ┌───────────────┴───────────────┐
    ↑                               ↑
SqlTxt.Cli                    SqlTxt.SampleApp
```

## Layers

### Parser Layer (SqlTxt.Parser)

- Tokenizes SQL-like text
- Produces command AST/DTO objects
- Reports parse errors with line/column when possible

### Engine Layer (SqlTxt.Engine)

- Receives command objects
- Validates against schema and rules
- Coordinates storage operations
- Returns execution results

### Storage Layer (SqlTxt.Storage)

- Manages directory structure
- Reads/writes schema, metadata, and row files
- Abstracts format versioning

### File System Layer

- Abstracted via `IFileSystemAccessor`
- Enables testing without real disk I/O
- Single-process friendly; basic file locking on writes

## Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `IDatabaseEngine` | Main entry point; execute commands, open databases |
| `ICommandParser` | Parse text to command objects |
| `ISchemaStore` | Schema read/write |
| `ITableDataStore` | Row data read/write |
| `IMetadataStore` | Metadata persistence |
| `IFileSystemAccessor` | File I/O abstraction |
| `IRowSerializer` / `IRowDeserializer` | Row format handling |

## Extensibility

- New command types: add to Contracts, Parser, Engine
- New storage formats: implement interfaces, use format version
- New column types: extend ColumnType, update serializers

## Reference

- [02-storage-format.md](02-storage-format.md) — On-disk layout
- [03-sql-subset.md](03-sql-subset.md) — Supported syntax
- [04-testing-strategy.md](04-testing-strategy.md) — Test approach
