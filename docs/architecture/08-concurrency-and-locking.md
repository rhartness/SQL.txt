# Concurrency and Locking

## Overview

SQL.txt supports multiple concurrent API calls. A lock/check system ensures data integrity when the engine is embedded in APIs or websites.

## Phase 1: Basic Locking

- **Single mutex per database** — One writer at a time
- **Readers block writers** — Simple implementation; safe
- **Scope:** Database-level or table-level mutex
- **Goal:** Prevent corruption; allow sequential concurrent requests

## Phase 2: Full Lock Manager

- **Per-database lock coordinator** — Tracks locks on tables/shards
- **Read lock (shared)** — Multiple readers allowed; blocks writers
- **Write lock (exclusive)** — Blocks readers and writers
- **Per-table or per-shard** — Granular locking for better concurrency

### WITH (NOLOCK)

SQL-like syntax for read-only queries:

```sql
SELECT * FROM Users WITH (NOLOCK);
```

- **Semantics:** Skip lock acquisition for SELECT
- **Trade-off:** Faster; allows dirty reads (may see uncommitted data)
- **Use case:** Read-only reporting, analytics

## Interface

```csharp
// Phase 1: simple
IDatabaseLockManager — AcquireWriteLock(), ReleaseWriteLock()

// Phase 2: full
IDataLockManager — AcquireReadLock(table), AcquireWriteLock(table), ReleaseLock()
// NOLOCK: skip AcquireReadLock when hint present
```

## Lock Scope

- **Phase 1:** Database-level or per-operation
- **Phase 2:** Per-table or per-shard for finer granularity

## Integration

- Engine acquires lock before read/write; releases after
- Lock manager injected via DI or created per database instance
- Timeout and deadlock handling: Phase 2 (optional)
