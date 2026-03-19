# ADR-009: Table-level and schema-level locking with FK ordering

## Status

Accepted (implementation in `SqlTxt.Engine` + `SqlTxt.Contracts`).

## Context

The concurrency document (`08-concurrency-and-locking.md`) described per-table locks before the implementation matched it. A single database-wide writer lock serialized all DML across tables and did not match the documented Phase 2 model.

## Decision

1. **Two lock granularities under one coordinator per normalized database directory:**
   - **Schema lock** — exclusive lock keyed only by database path (`AcquireWriteLockAsync`). Used for DDL (`CREATE TABLE`, `CREATE INDEX`), `RebalanceTableAsync`, and any operation that must exclude concurrent DML across the database.
   - **Table locks** — one reader–writer pair per `(databasePath, tableName)` for DML and `SELECT`.

2. **FK-safe acquisition order** — For statements that touch multiple tables, acquire all needed locks in **ascending order by table name** (ordinal case-insensitive). Typical DML:
   - **Shared** locks on every **referenced parent** table (from `ForeignKeyDefinition.ReferencedTable`).
   - **Exclusive** lock on the **target** table of the command.
   - If a table appears in both sets, the exclusive lock supersedes read intent for that table.

3. **Release order** — Release locks in **reverse** acquisition order (standard practice).

4. **`WITH (NOLOCK)`** — Skips table read locks; does not skip schema lock if the engine ever runs DDL in the same call (not applicable today for pure SELECT).

## Consequences

- **Positive:** Concurrent writers on **different tables** can proceed without serializing on the whole database, subject to FK read locks.
- **Positive:** Deadlock avoidance for FK-heavy workloads without a global lock order across unrelated tables, by fixing total order on the **specific** tables touched by one command.
- **Negative:** Cross-table transactions (Phase 5) will need **lock set** expansion and optional deadlock detection if dynamic SQL adds tables mid-transaction.

## Alternatives considered

- **Single DB RW lock only** — Simple but contradicts docs and limits throughput; rejected.
- **Shard-level locks** — Finer than table but increases complexity while index/shard metadata remains table-scoped; deferred.
