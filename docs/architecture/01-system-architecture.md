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
    ┌───────────────┬───────────────┬───────────────┐
    ↑               ↑               ↑               ↑
SqlTxt.Cli    SqlTxt.SampleApp  SqlTxt.Service  NuGet (SqlTxt)
```

- **SqlTxt.Service** — Phase 2; installable service
- **NuGet** — Package Engine for embedding in APIs, websites

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

### Concurrency Layer

- Lock coordinator for concurrent API calls
- Phase 1: basic single-writer lock
- Phase 2: read/write locks; WITH (NOLOCK)

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
| `IDatabaseLockManager` | Lock coordination (Phase 1); `IDataLockManager` (Phase 2) |

### Future Components

- **Statistics (Phase 7):** Histogram storage, cardinality estimation, optimizer integration. Metadata slots reserved in ~System.
- **Rebalance API:** `RebalanceTableAsync(tableName)` — redistributes rows across shards; exposed via Engine, Service (`POST /rebalance/{tableName}`), and CLI (`sqltxt rebalance --db ./Db --table Users`).

## Extensibility

- New command types: add to Contracts, Parser, Engine
- New storage formats: implement interfaces, use format version
- New column types: extend ColumnType, update serializers

## Reference

- [02-storage-format.md](02-storage-format.md) — On-disk layout
- [11-sql2023-mapping.md](11-sql2023-mapping.md) — SQL:2023 feature mapping
- [03-sql-subset.md](03-sql-subset.md) — Supported syntax
- [04-testing-strategy.md](04-testing-strategy.md) — Test approach
- [07-api-and-deployment.md](07-api-and-deployment.md) — Build types, API
- [08-concurrency-and-locking.md](08-concurrency-and-locking.md) — Locking
- [09-wasm-storage.md](09-wasm-storage.md) — WASM-compatible storage
- [10-performance-and-efficiency.md](10-performance-and-efficiency.md) — Speed, memory, durability
