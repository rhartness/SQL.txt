# SQL:2023 Compliance Roadmap

## Purpose

This document references ISO/IEC 9075:2023 (SQL:2023) and maps all SQL:2023 features to SQL.txt phases. The goal is **100% SQL:2023 compliance per phase** for features within each phase's scope. Features outside the phased scope are documented here for future implementation.

**Reference:** [SQL:2023 (modern-sql.com)](https://modern-sql.com/standard/2023), [ISO/IEC 9075-2:2023](https://www.iso.org/standard/76584.html)

---

## SQL:2023 Feature Registry

The comprehensive list of all SQL:2023 features with phase assignments and spec references is in [01-sql2023-feature-registry.md](01-sql2023-feature-registry.md). Individual phase documents reference this registry for full spec/language compliance.

---

## Execution Order

Phases execute in this order:

1. **Phase 0** — Foundation (CREATE DATABASE, storage, sharding)
2. **Phase 1** — Core DML/DDL (CREATE TABLE, INSERT, SELECT, UPDATE, DELETE)
3. **Phase 2** — Integrity and Indexes (PK, FK, UNIQUE, CREATE INDEX)
4. **Phase 3** — VARCHAR and String Types

5. **Phase 3.5** — Storage & ingest efficiency (engine/storage implementation: batched multi-row INSERT I/O, true append semantics, sorted on-disk indexes per ADR-008). Not a new SQL:2023 clause; improves execution of existing INSERT.

6. **Phase 4** — Query Enrichment (JOINs, aggregates, ORDER BY, GROUP BY, subqueries)
7. **CTE Phase** — Common Table Expressions (WITH, WITH RECURSIVE)
8. **Phase 5** — Schema Evolution and Transactions (ALTER TABLE, BEGIN/COMMIT/ROLLBACK)
9. **Phase 6** — Programmability (Views, Procedures, Functions)
10. **Phase 7** — Statistics (CREATE STATISTICS)

---

## Phased Feature Mapping (In Scope)

| Phase | SQL:2023 Features | Status |
|-------|-------------------|--------|
| Phase 1 | Core DML/DDL (CREATE TABLE, INSERT, SELECT, UPDATE, DELETE); fixed-width types | Done |
| Phase 2 | PRIMARY KEY, FOREIGN KEY, UNIQUE, CREATE INDEX | Done |
| Phase 3 | VARCHAR, T055 (LPAD, RPAD), T056 (multi-char TRIM), T062 (CHAR_LENGTH, OCTET_LENGTH), T081 (optional VARCHAR max) | Done |
| Phase 3.5 | INSERT execution efficiency (batched validation/writes, sorted index files); see [Phase3_5_Storage_Efficiency_Plan.md](../plans/Phase3_5_Storage_Efficiency_Plan.md) | Next |
| Phase 4 | F401 (JOIN), F406 (FULL OUTER), F407 (CROSS), F405 (NATURAL); aggregates; ORDER BY, GROUP BY, HAVING; subqueries; T626 (ANY_VALUE) | After 3.5 |
| CTE Phase | WITH clause; recursive CTE | Planned |
| Phase 5 | F387 (ALTER COLUMN), F388 (ADD/DROP CONSTRAINT, RENAME); F112–F114 (isolation levels) | Planned |
| Phase 6 | CREATE VIEW; CREATE PROCEDURE; CREATE FUNCTION | Planned |
| Phase 7 | CREATE STATISTICS (implementation-defined) | Planned |

---

## CREATE DATABASE / CREATE TABLE Completeness

### CREATE DATABASE (Implementation-Defined)

- [x] `CREATE DATABASE name`
- [x] `WITH (defaultMaxShardSize=...)`
- [x] `WITH (storageBackend=text|binary)`
- [x] `WITH (numberFormat=...)`
- [x] `WITH (textEncoding=...)`

### CREATE TABLE (SQL:2023 Core Subset)

- [x] `CREATE TABLE name (column_def, ...)`
- [x] Column types: CHAR(n), VARCHAR(n), INT, TINYINT, BIGINT, BIT, DECIMAL(p,s)
- [x] PRIMARY KEY (column or table level)
- [x] FOREIGN KEY ... REFERENCES
- [x] UNIQUE (column or table level)
- [ ] DEFAULT expression (Phase 5 with ALTER)
- [ ] NOT NULL (Phase 1+ enhancement)
- [ ] CHECK constraint (future)
- [ ] CREATE TABLE ... AS SELECT (future)
- [ ] CREATE TABLE ... LIKE (future)

---

## Deferred / Future Phases

JSON, Arrays, BLOB/CLOB, Triggers, Property Graph Queries, and other deferred features are documented in [01-sql2023-feature-registry.md](01-sql2023-feature-registry.md).

---

## SQL:2023 Features Not Yet in Phases (Deferred)

These features exist in SQL:2023 but are not yet assigned to a phase. They are documented for future roadmap consideration.

### JSON (SQL:2023 Part 2 / SQL/Foundation)

| Feature ID | Name | Notes |
|------------|------|-------|
| T801 | JSON data type | Future phase |
| T802 | Enhanced JSON data type | Future phase |
| T803 | String-based JSON | Future phase |
| T840 | Hex integer literals in SQL/JSON path | Future phase |
| T851 | SQL/JSON optional keywords | Future phase |
| T860–T864 | SQL/JSON simplified accessor | Future phase |
| T865–T878 | SQL/JSON item methods | Future phase |
| T879–T882 | JSON in equality, grouping, ordering | Future phase |

### Arrays

| Feature ID | Name | Notes |
|------------|------|-------|
| S090 | Minimal array support | Future phase |
| S093 | Arrays of distinct types | Future phase |
| S099 | Array expressions | Future phase |
| S203 | Array parameters | Future phase |
| S204 | Array as result type | Future phase |

### Large Objects (BLOB/CLOB)

| Feature ID | Name | Notes |
|------------|------|-------|
| T045 | BLOB data type | Future phase |
| T046 | CLOB data type | Future phase |
| T047–T050 | BLOB/CLOB operations, concatenation, locators | Future phase |
| T039, T040 | CLOB locator, concatenation | Future phase |

### Triggers

| Feature ID | Name | Notes |
|------------|------|-------|
| T200 | Trigger DDL | Future phase |
| T214 | BEFORE triggers | Future phase |
| T215 | AFTER triggers | Future phase |
| T216–T218 | Trigger conditions, privilege, ordering | Future phase |

### Other SQL:2023 Features

| Feature ID | Name | Notes |
|------------|------|-------|
| B030 | Enhanced dynamic SQL | Deferred |
| B036 | Describe input statement | Deferred |
| F035–F038 | REVOKE with CASCADE, etc. | Deferred |
| F120, F124 | Get diagnostics, SET TRANSACTION DIAGNOSTICS | Deferred |
| F292 | UNIQUE null treatment (NULLS DISTINCT/NOT DISTINCT) | Phase 2+ enhancement |
| F303, F305 | INTERSECT DISTINCT, INTERSECT ALL | Phase 4+ enhancement |
| F432–F438 | FETCH variants, scrollable cursors | Deferred |
| F832, F833 | Updatable scrollable/ordered cursors | Deferred |
| F868 | ORDER BY in grouped table | Phase 4 enhancement |
| T054 | GREATEST and LEAST | Phase 3/4 enhancement |
| T133 | Enhanced cycle mark values | CTE phase enhancement |
| T262 | Multiple server transactions | Deferred |
| T661 | Non-decimal integer literals | Parser enhancement |
| T662 | Underscores in numeric literals | Parser enhancement |
| T670 | Schema and data statement mixing | Deferred |

### Property Graph Queries (SQL/PGQ)

SQL:2023 Part 16 introduces Property Graph Queries. Not in current roadmap; document for future consideration.

---

## Storage Backend (Dual Mode)

SQL.txt supports two storage backends, chosen at database creation:

- **text** — Human-readable files (`.txt`); for learning and inspection
- **binary** — Compact binary files (`.bin`, `.idx`); for performance

Both backends support the same SQL:2023 feature set. See [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md).

---

## References

- [01-sql2023-feature-registry.md](01-sql2023-feature-registry.md) — Full SQL:2023 feature list with phase assignments
- [11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md) — Per-phase mapping
- [adr-005-sql2023-alignment.md](../decisions/adr-005-sql2023-alignment.md) — Alignment decision
