# ADR-004: API, Service, NuGet, and Concurrency

## Status

Accepted

## Context

SQL.txt must support multiple deployment forms and concurrent access when embedded in APIs or websites. Users need an async API, lock management, and optional NOLOCK for read-only queries.

## Decisions

### 1. Three Build Types

- **CLI executable** — Standalone .exe (Windows) or binary (macOS, Linux). Current SqlTxt.Cli.
- **Installable Service** — Windows Service, systemd (Linux), launchd (macOS). New SqlTxt.Service project (Phase 2).
- **NuGet API DLL** — Library package consumable by other apps, APIs, websites. Package Engine + dependencies.

### 2. Async API

All database operations exposed via async methods. `IDatabaseEngine` (or equivalent) provides:

- `Task<EngineResult> ExecuteAsync(string sql, CancellationToken ct = default)`
- `Task<QueryResult> ExecuteQueryAsync(string sql, CancellationToken ct = default)`
- `Task OpenAsync(string path, CancellationToken ct = default)`

### 3. Lock Strategy

**Phase 1:** Single mutex per database. One writer at a time. Readers block writers. Simple but safe.

**Phase 2:** Reader-writer lock. Multiple concurrent reads. Writers get exclusive lock. `WITH (NOLOCK)` skips lock for SELECT (faster; allows dirty reads).

### 4. WITH (NOLOCK)

SQL-like syntax: `SELECT ... FROM table WITH (NOLOCK)`. Phase 2. Skips lock acquisition for read-only queries.

### 5. NuGet Package

- Package id: `SqlTxt`
- Contents: Engine, Contracts, Core, Storage, Parser
- Consumers: CLI, SampleApp, Service, external apps

### 6. Service Transport

Defer to Phase 2 design. Options: HTTP REST, gRPC, named pipes.

## Consequences

- Engine must be designed for async from Phase 1
- Lock coordinator interface needed; Phase 1 uses simple implementation
- Parser must support WITH (NOLOCK) in Phase 2
- NuGet packaging adds build/publish step
