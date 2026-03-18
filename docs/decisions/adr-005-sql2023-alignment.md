# ADR-005: SQL:2023 Alignment

## Status

Accepted

## Context

SQL.txt aims to implement a SQL-like database engine. Aligning with the ISO/IEC 9075:2023 (SQL:2023) standard provides a clear target for syntax and semantics, enables incremental compliance, and helps users understand which features are supported.

## Decision

SQL.txt will target **SQL:2023** (ISO/IEC 9075-2:2023 Foundation) as the specification reference. Each phase implements the applicable subset of SQL:2023 features incrementally.

### Per-Phase Mapping

| Phase | SQL:2023 Features |
|-------|-------------------|
| Phase 1 | Core DML/DDL (foundational statements) |
| Phase 2 | PRIMARY KEY, FOREIGN KEY, UNIQUE, indexes |
| Phase 3 | VARCHAR, string types (T055, T056, T062, T081) |
| Phase 4 | JOINs (F405–F407), aggregates, ORDER BY, GROUP BY |
| CTE Phase | WITH clause (Common Table Expressions) |
| Phase 5 | ALTER TABLE (F387, F388), transactions (F112–F114) |
| Phase 6 | Views, stored procedures, functions |
| Phase 7 | Statistics (implementation-defined; no direct SQL:2023 equivalent) |

### Documentation

A dedicated mapping document (`docs/architecture/11-sql2023-mapping.md`) will track feature IDs and implementation status per phase.

## Consequences

- Parser and engine design should align with SQL:2023 semantics where applicable
- Deviations must be documented with rationale
- Future phases can reference the standard for unambiguous behavior
