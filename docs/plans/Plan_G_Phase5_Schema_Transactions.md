# Plan G: Phase 5 — Schema Evolution and Transactions (SQL:2023)

**Status:** Pending  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan F complete

**Reference:** [docs/specifications/02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md), [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)

---

## Scope

- ALTER TABLE (F387 ADD/DROP COLUMN, F388 RENAME)
- Transactions: BEGIN/COMMIT/ROLLBACK (F112–F114 isolation levels)

---

## Wave 1: ALTER TABLE Parser

### 1.1 ADD COLUMN

- `ALTER TABLE t ADD COLUMN c VARCHAR(10);`
- `ALTER TABLE t ADD COLUMN c INT DEFAULT 0;`

### 1.2 DROP COLUMN

- `ALTER TABLE t DROP COLUMN c;`

### 1.3 RENAME

- `ALTER TABLE t RENAME TO t2;`
- `ALTER TABLE t RENAME COLUMN c TO c2;` (F388)

### 1.4 ADD/DROP CONSTRAINT (F388)

- `ALTER TABLE t ADD CONSTRAINT ...`
- `ALTER TABLE t DROP CONSTRAINT ...`

---

## Wave 2: ALTER TABLE Execution

### 2.1 ADD COLUMN

- Update schema; backfill new column (default or NULL) for existing rows
- Streaming/copy-on-write for large tables; both text and binary backends

### 2.2 DROP COLUMN

- Update schema; rewrite rows without dropped column
- Streaming/copy-on-write

### 2.3 RENAME

- Update schema and metadata; rename files if needed (table folder, indexes)
- Column rename: schema update only

### 2.4 Constraint Changes

- ADD/DROP PRIMARY KEY, UNIQUE, FOREIGN KEY
- Validate existing data; update indexes

---

## Wave 3: Transactions

### 3.1 Parser

- `BEGIN [TRANSACTION];`
- `COMMIT [TRANSACTION];`
- `ROLLBACK [TRANSACTION];`
- `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED | READ COMMITTED | REPEATABLE READ` (F112–F114)

### 3.2 Execution

- Transaction context: track uncommitted writes
- COMMIT: flush writes; release locks
- ROLLBACK: discard uncommitted writes
- Isolation: Phase 1 may implement READ COMMITTED; document isolation semantics

### 3.3 Lock Integration

- Transactions acquire and hold locks until COMMIT/ROLLBACK
- Integrate with existing IDatabaseLockManager / IDataLockManager

---

## Wave 4: Tests and Documentation

### 4.1 Unit and Integration Tests

- All ALTER TABLE variants
- BEGIN/COMMIT/ROLLBACK; isolation behavior
- Concurrent transactions (if supported)

### 4.2 Documentation

- Update 11-sql2023-mapping.md: F387, F388, F112–F114
- Update README Roadmap
- Transaction isolation documentation
- Examples for CLI, WASM, Embedding

---

## Plan Execution Rules (Apply on Completion)

1. Update README Roadmap table
2. Update 11-sql2023-mapping.md
3. Update CLI reference, Getting Started
4. Provide examples for CLI, WASM, Embedding

---

## Deliverable

ALTER TABLE (ADD/DROP COLUMN, RENAME, ADD/DROP CONSTRAINT). Transactions with BEGIN/COMMIT/ROLLBACK. Isolation levels F112–F114. Enterprise-grade schema evolution with streaming/copy-on-write.
