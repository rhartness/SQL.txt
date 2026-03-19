# SQL:2023 Feature Registry

## Purpose

This document is the comprehensive reference for all SQL:2023 features from ISO/IEC 9075-2:2023 (SQL/Foundation). Each feature is assigned to a phase, status, and spec reference. Individual phase documents reference this registry for full spec/language compliance.

**Reference:** [ISO/IEC 9075-2:2023](https://www.iso.org/standard/76584.html) — SQL/Foundation  
**Reference:** [SQL:2023 (modern-sql.com)](https://modern-sql.com/standard/2023)

---

## Core Features (Mandatory in SQL Standard)

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| Core | CREATE TABLE | 1 | Done | Clause 4 (Schema definition) |
| Core | INSERT statement | 1 | Done | Clause 14 (Data manipulation) |
| Core | Query specification (SELECT) | 1 | Done | Clause 7 (Query) |
| Core | UPDATE statement | 1 | Done | Clause 14 |
| Core | DELETE statement | 1 | Done | Clause 14 |
| Core | Data types: CHAR, VARCHAR, INT, DECIMAL, etc. | 1, 3 | Done | Clause 6 (Data types) |
| Core | PRIMARY KEY constraint | 2 | Done | Clause 11 (Constraints) |
| Core | FOREIGN KEY constraint | 2 | Done | Clause 11 |
| Core | UNIQUE constraint | 2 | Done | Clause 11 |
| Core | NULL / NOT NULL | 1 | Done | Clause 11 |
| Core | Aggregates (COUNT, SUM, AVG, MIN, MAX) | 4 | Planned | Clause 9 (Expressions) |
| Core | ORDER BY | 4 | Planned | Clause 7 |
| Core | GROUP BY | 4 | Planned | Clause 7 |
| Core | HAVING | 4 | Planned | Clause 7 |
| Core | Subqueries (IN, EXISTS, scalar) | 4 | Planned | Clause 7 |
| Core | CREATE VIEW | 6 | Planned | Clause 4 |
| Core | CREATE PROCEDURE | 6 | Planned | Clause 11 (Routines) |
| Core | CREATE FUNCTION | 6 | Planned | Clause 11 |

---

## Implementation-Defined Features

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| Impl | CREATE DATABASE | 0 | Done | Not in standard; engine-specific |
| Impl | CREATE INDEX | 2 | Done | Standard does not mandate syntax |
| Impl | CREATE STATISTICS | 7 | Planned | No direct SQL:2023 equivalent; adr-006 |
| ID106 | Unique null treatment when unspecified | 2 | Planned | Implementation-defined |
| IA201 | Rows with all NULLs: distinct or not | 2 | Planned | Implementation-defined |

---

## Optional Features (SQL:2023 New or Renumbered)

### Phase 2 — Integrity and Indexes

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| F292 | UNIQUE null treatment (NULLS DISTINCT/NOT DISTINCT) | 2 | Enhancement | Clause 11 |

### Phase 3 — VARCHAR and String Types

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| T055 | String padding functions (LPAD, RPAD) | 3 | Planned | Clause 9 |
| T056 | Multi-character TRIM function | 3 | Planned | Clause 9 |
| T062 | Character length units (CHAR_LENGTH, OCTET_LENGTH) | 3 | Planned | Clause 9 |
| T081 | Optional string types maximum length (VARCHAR without max) | 3 | Planned | Clause 6 |
| T054 | GREATEST and LEAST | 3 | Enhancement | Clause 9 |

### Phase 4 — Query Enrichment

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| F401 | JOIN (INNER, LEFT OUTER) | 4 | Planned | Clause 7 |
| F405 | NATURAL JOIN | 4 | Planned | Clause 7 |
| F406 | FULL OUTER JOIN | 4 | Planned | Clause 7 |
| F407 | CROSS JOIN | 4 | Planned | Clause 7 |
| T626 | ANY_VALUE | 4 | Planned | Clause 9 |
| F868 | ORDER BY in grouped table | 4 | Planned | Clause 7 |
| F303 | INTERSECT DISTINCT | 4 | Enhancement | Clause 7 |
| F305 | INTERSECT ALL | 4 | Enhancement | Clause 7 |
| T661 | Non-decimal integer literals | 4 | Parser | Clause 5 |
| T662 | Underscores in numeric literals | 4 | Parser | Clause 5 |

### CTE Phase

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| — | WITH clause (non-recursive CTE) | CTE | Planned | Clause 7.16 |
| — | Recursive CTE (WITH RECURSIVE) | CTE | Planned | Clause 7.16 |
| T133 | Enhanced cycle mark values | CTE | Enhancement | Clause 7.16 |

### Phase 5 — Schema Evolution and Transactions

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| F387 | ALTER TABLE: ALTER COLUMN clause | 5 | Planned | Clause 11 |
| F388 | ALTER TABLE: ADD/DROP CONSTRAINT, RENAME | 5 | Planned | Clause 11 |
| F112 | Isolation level READ UNCOMMITTED | 5 | Planned | Clause 15 |
| F113 | Isolation level READ COMMITTED | 5 | Planned | Clause 15 |
| F114 | Isolation level REPEATABLE READ | 5 | Planned | Clause 15 |

### Phase 6 — Programmability

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| — | CREATE VIEW | 6 | Planned | Clause 4 |
| — | CREATE PROCEDURE | 6 | Planned | Clause 11 |
| — | CREATE FUNCTION | 6 | Planned | Clause 11 |

---

## Deferred Features (Future Phases)

### Dynamic SQL and Diagnostics

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| B030 | Enhanced dynamic SQL | Future | Deferred | — |
| B036 | Describe input statement | Future | Deferred | — |
| F120 | Get diagnostics statement | Future | Deferred | — |
| F124 | SET TRANSACTION DIAGNOSTICS SIZE | Future | Deferred | — |

### Privileges

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| F035 | REVOKE with CASCADE | Future | Deferred | — |
| F036 | REVOKE by non-owner | Future | Deferred | — |
| F037 | REVOKE: GRANT OPTION FOR | Future | Deferred | — |
| F038 | REVOKE of WITH GRANT OPTION | Future | Deferred | — |

### Cursors and Fetch

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| F432 | FETCH with explicit NEXT | Future | Deferred | — |
| F433 | FETCH FIRST | Future | Deferred | — |
| F434 | FETCH LAST | Future | Deferred | — |
| F435 | FETCH PRIOR | Future | Deferred | — |
| F436 | FETCH ABSOLUTE | Future | Deferred | — |
| F437 | FETCH RELATIVE | Future | Deferred | — |
| F438 | Scrollable cursors | Future | Deferred | — |
| F832 | Updatable scrollable cursors | Future | Deferred | — |
| F833 | Updatable ordered cursors | Future | Deferred | — |

### Triggers

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| T200 | Trigger DDL | Future | Deferred | — |
| T214 | BEFORE triggers | Future | Deferred | — |
| T215 | AFTER triggers | Future | Deferred | — |
| T216 | Trigger search condition | Future | Deferred | — |
| T217 | TRIGGER privilege | Future | Deferred | — |
| T218 | Multiple triggers ordering | Future | Deferred | — |

### Large Objects (BLOB/CLOB)

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| T039 | CLOB locator: non-holdable | Future | Deferred | — |
| T040 | Concatenation of CLOBs | Future | Deferred | — |
| T045 | BLOB data type | Future | Deferred | — |
| T046 | CLOB data type | Future | Deferred | — |
| T047 | BLOB operations (POSITION, OCTET_LENGTH, TRIM, SUBSTRING) | Future | Deferred | — |
| T048 | Concatenation of BLOBs | Future | Deferred | — |
| T049 | BLOB locator: non-holdable | Future | Deferred | — |
| T050 | CLOB operations | Future | Deferred | — |

### Arrays

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| S090 | Minimal array support | Future | Deferred | — |
| S093 | Arrays of distinct types | Future | Deferred | — |
| S099 | Array expressions | Future | Deferred | — |
| S203 | Array parameters | Future | Deferred | — |
| S204 | Array as result type | Future | Deferred | — |

### JSON (SQL:2023 Part 2)

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| T801 | JSON data type | Future | Deferred | — |
| T802 | Enhanced JSON data type | Future | Deferred | — |
| T803 | String-based JSON | Future | Deferred | — |
| T840 | Hex integer literals in SQL/JSON path | Future | Deferred | — |
| T851 | SQL/JSON optional keywords | Future | Deferred | — |
| T860 | SQL/JSON simplified accessor: column reference | Future | Deferred | — |
| T861 | SQL/JSON: case-sensitive member accessor | Future | Deferred | — |
| T862 | SQL/JSON: wildcard member accessor | Future | Deferred | — |
| T863 | SQL/JSON: single-quoted string as member | Future | Deferred | — |
| T864 | SQL/JSON simplified accessor | Future | Deferred | — |
| T865–T878 | SQL/JSON item methods | Future | Deferred | — |
| T879 | JSON in equality operations | Future | Deferred | — |
| T880 | JSON in grouping operations | Future | Deferred | — |
| T881 | JSON in ordering operations | Future | Deferred | — |
| T882 | JSON in multiset element grouping | Future | Deferred | — |

### Other

| Feature ID | Name | Phase | Status | Spec Reference |
|------------|------|-------|--------|----------------|
| T262 | Multiple server transactions | Future | Deferred | — |
| T670 | Schema and data statement mixing | Future | Deferred | — |
| T627 | Window framed COUNT DISTINCT | Future | Deferred | — |

### Property Graph Queries (SQL/PGQ)

SQL:2023 Part 16. Not in current roadmap; document for future consideration.

---

## Phase Summary

| Phase | Features Count | Status |
|-------|----------------|--------|
| Phase 0 | CREATE DATABASE, storage, sharding | Done |
| Phase 1 | Core DML/DDL, data types | Done |
| Phase 2 | PK, FK, UNIQUE, CREATE INDEX, F292 | Done |
| Phase 3 | VARCHAR, T055, T056, T062, T081, T054 | Done |
| Phase 4 | F401, F405–F407, aggregates, ORDER BY, GROUP BY, HAVING, subqueries, T626, F868, F303, F305, T661, T662 | Planned |
| CTE Phase | WITH, WITH RECURSIVE, T133 | Planned |
| Phase 5 | F387, F388, F112–F114 | Planned |
| Phase 6 | CREATE VIEW, PROCEDURE, FUNCTION | Planned |
| Phase 7 | CREATE STATISTICS (impl-defined) | Planned |
| Future | JSON, Arrays, BLOB/CLOB, Triggers, etc. | Deferred |

---

## References

- [00-sql2023-compliance-roadmap.md](00-sql2023-compliance-roadmap.md) — Master roadmap
- [11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md) — Per-phase mapping
- [adr-005-sql2023-alignment.md](../decisions/adr-005-sql2023-alignment.md) — Alignment decision
