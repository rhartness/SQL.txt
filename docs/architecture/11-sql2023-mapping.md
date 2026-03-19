# SQL:2023 Feature Mapping

## Purpose

This document maps SQL.txt phases to ISO/IEC 9075-2:2023 (SQL:2023 Foundation) feature IDs. Each phase implements the applicable subset incrementally. See [adr-005-sql2023-alignment.md](../decisions/adr-005-sql2023-alignment.md).

## Phase 1 — Core DML/DDL

| Statement | SQL:2023 Basis | Notes |
|-----------|----------------|-------|
| CREATE DATABASE | Implementation-defined | Not in SQL standard; engine-specific |
| CREATE TABLE | Core schema definition | Subset: fixed-width types only |
| INSERT | Insert statement | Multi-value `VALUES`; Phase 3.5 optimizes batched I/O and indexes (see [Phase3_5_Storage_Efficiency_Plan.md](../plans/Phase3_5_Storage_Efficiency_Plan.md)) |
| SELECT | Query specification | Single-table; equality predicate only |
| UPDATE | Update statement | Single-table; equality predicate |
| DELETE | Delete statement | Single-table; equality predicate |

**Data types:** CHAR(n), INT, TINYINT, BIGINT, BIT, DECIMAL(p,s) — foundational numeric and character types.

## Phase 2 — Indexes, PK, FK, Constraints

| Feature | SQL:2023 Basis | Notes |
|---------|----------------|-------|
| PRIMARY KEY | Unique constraint | Column or table level |
| FOREIGN KEY | Referential integrity | No CASCADE in Phase 2 |
| UNIQUE | Unique constraint | Column or table level |
| CREATE INDEX | Implementation-defined | Index creation; standard does not mandate syntax |

**Index format:** Value|ShardId|_RowId per [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md).

## Concurrency — Table / schema locks and MVCC (implementation milestones)

| Topic | Notes |
|-------|--------|
| Schema lock | Exclusive for DDL / catalog mutations; shared (“read”) for DML and snapshot `SELECT` entry |
| Per-table locks | Ordered acquisition; parallel DML on different tables; FK-safe ordering per [adr-009-table-schema-locking.md](../decisions/adr-009-table-schema-locking.md) |
| `WITH (NOLOCK)` | Dirty / non-snapshot reads; documented in [08-concurrency-and-locking.md](08-concurrency-and-locking.md) |
| MVCC | Row `xmin` / `xmax`, committed snapshot for default `SELECT`; pre-release format per [adr-010-mvcc-row-versions.md](../decisions/adr-010-mvcc-row-versions.md) |
| Isolation SQL | `SET TRANSACTION` / level names aligned with Phase 5; engine prepares with auto-commit xids |

## Phase 3 — VARCHAR and String Types

| Feature | SQL:2023 ID | Notes |
|---------|--------------|-------|
| VARCHAR | Core character types | Variable-length; implemented |
| T055 | String padding functions | LPAD, RPAD |
| T056 | Multi-character TRIM | TRIM with multiple characters |
| T062 | Character length units | CHAR_LENGTH, OCTET_LENGTH |
| T081 | Optional string max length | VARCHAR without explicit max |

## Phase 3.5 — Storage & ingest efficiency

| Topic | Notes |
|-------|--------|
| Multi-row INSERT | Same SQL as Phase 1; engine batches validation, row appends (per shard), and index writes |
| Indexes | Sorted on-disk lines, binary search (ADR-008) |

Not a separate SQL:2023 clause; implementation milestone before Phase 4. See [Phase3_5_Storage_Efficiency_Plan.md](../plans/Phase3_5_Storage_Efficiency_Plan.md).

## Phase 4 — Query Enrichment

| Feature | SQL:2023 ID | Notes |
|---------|--------------|-------|
| INNER JOIN | F401 (JOIN) | Two-table initially |
| LEFT OUTER JOIN | F401 | |
| FULL OUTER JOIN | F406 | Optional |
| CROSS JOIN | F407 | |
| NATURAL JOIN | F405 | Optional |
| Aggregates | Core | COUNT, SUM, AVG, MIN, MAX |
| ORDER BY | Core | Sort specification |
| GROUP BY | Core | Grouping |
| HAVING | Core | Group filter |
| Subqueries | Core | IN, EXISTS, scalar |

## CTE Phase — Common Table Expressions

| Feature | SQL:2023 Basis | Notes |
|---------|----------------|-------|
| WITH clause | Standard CTE | Non-recursive |
| Recursive CTE | Standard | UNION ALL anchor + recursive |

```sql
WITH cte AS (SELECT ...) SELECT * FROM cte;
WITH RECURSIVE cte AS (...) SELECT * FROM cte;
```

## Phase 5 — Schema Evolution and Transactions

| Feature | SQL:2023 ID | Notes |
|---------|--------------|-------|
| ALTER TABLE ADD COLUMN | F387 | |
| ALTER TABLE DROP COLUMN | F387 | |
| ALTER TABLE RENAME | F388 | |
| BEGIN/COMMIT/ROLLBACK | F112–F114 | Isolation levels; MVCC + snapshot groundwork precedes full multi-statement sessions |

## Phase 6 — Programmability

| Feature | SQL:2023 Basis | Notes |
|---------|----------------|-------|
| CREATE VIEW | View definition | Stored SELECT |
| CREATE PROCEDURE | Module | Stored procedures |
| CREATE FUNCTION | Module | Scalar functions |

## Phase 7 — Statistics

| Feature | SQL:2023 | Notes |
|---------|----------|-------|
| CREATE STATISTICS | Implementation-defined | No direct SQL:2023 equivalent |
| Histogram | — | SQL Server–style; see [adr-006-statistics-design.md](../decisions/adr-006-statistics-design.md) |

## Reference

- [SQL:2023 (modern-sql.com)](https://modern-sql.com/standard/2023)
- [ISO/IEC 9075-2:2023](https://www.iso.org/standard/76584.html) — SQL/Foundation
