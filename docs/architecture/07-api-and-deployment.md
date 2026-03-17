# API and Deployment

## Overview

SQL.txt supports three build types: CLI executable, installable Service, and NuGet API DLL. All consume the same Engine. The API is async and supports concurrent access via a lock coordinator.

**WASM storage:** The CLI supports `--wasm` mode for browser-compatible storage (single `.wasmdb` file). See [09-wasm-storage.md](09-wasm-storage.md) for design and future browser deployment.

## Build Types

### 1. CLI Executable

- **Project:** SqlTxt.Cli
- **Output:** Standalone .exe (Windows) or binary (macOS, Linux)
- **Use:** Command-line interaction, scripts, local admin

### 2. NuGet API DLL

- **Package:** SqlTxt
- **Contents:** SqlTxt.Engine, SqlTxt.Contracts, SqlTxt.Core, SqlTxt.Storage, SqlTxt.Parser
- **Use:** Embed in other APIs, websites, applications. Add package reference; all DB functions accessible via async API.

**Consumer example:**

```csharp
var engine = await SqlTxt.OpenAsync("./MyDatabase");
await engine.ExecuteAsync("CREATE TABLE Users (Id CHAR(10), Name CHAR(50))");
await engine.ExecuteAsync("INSERT INTO Users (Id, Name) VALUES ('1', 'Alice')");
var result = await engine.ExecuteQueryAsync("SELECT * FROM Users");
```

### 3. Installable Service

- **Project:** SqlTxt.Service (Phase 2)
- **Output:** Windows Service, systemd unit (Linux), launchd (macOS)
- **Use:** Long-running database server; accepts requests from external clients
- **Transport:** Defer to Phase 2 (HTTP REST, gRPC, or named pipes)

## API Surface

All database operations exposed via `IDatabaseEngine` (or equivalent):

| Method | Purpose |
|--------|---------|
| `OpenAsync(path, ct)` | Open database at path |
| `ExecuteAsync(sql, ct)` | Execute non-query statement (CREATE, INSERT, UPDATE, DELETE) |
| `ExecuteQueryAsync(sql, ct)` | Execute SELECT; return rows |

All methods are async and accept `CancellationToken`.

## Concurrency

See [08-concurrency-and-locking.md](08-concurrency-and-locking.md). Multiple concurrent API calls are supported. Lock coordinator ensures data integrity.
