# Product Specification

## Purpose

**SQL.txt** is a lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in human-readable text files.

## Scope

### In Scope

- Dual storage backends (text, binary) — chosen at database creation; text for learning/inspection, binary for performance
- Human-readable on-disk storage (text backend)
- Lightweight local embedded usage
- Phased implementation (Stage 0 → Phase 1 → Phase 2 → Phase 3 → Phase 4 → CTE → Phase 5 → Phase 6 → Phase 7)
- Single-table CRUD in Phase 1
- Fixed-width fields (CHAR) in Phase 1
- Indexes, PK/FK in Phase 2
- VARCHAR in Phase 3
- JOINs, aggregates, ORDER BY, GROUP BY, subqueries in Phase 4
- ALTER TABLE, transactions in Phase 5
- Views, stored procedures, functions in Phase 6
- CTE (Common Table Expressions) — dedicated phase after Phase 4
- Statistics (SQL Server–style) in Phase 7
- SQL:2023 alignment — each phase implements applicable standard features
- Configurable sharding — database default 20MB; rebalance API

### Out of Scope (Initial Releases)

- Enterprise-scale performance
- Full SQL standard support
- Multi-version concurrency control
- Complex joins in Phase 1
- Distributed storage
- Advanced functions, aggregates, subqueries in Phase 1

## Phased Roadmap

| Phase | Deliverables |
|-------|--------------|
| Stage 0 | Solution scaffolding, design docs, Cursor guidance |
| Phase 1 | CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width only; SQL:2023 subset |
| Phase 2 | Indexes, PK/FK, constraints, relational metadata; STOC; configurable sharding (20MB default) |
| Phase 3 | VARCHAR, variable-width fields, storage evolution |
| Phase 4 | JOINs, aggregates, ORDER BY, GROUP BY, subqueries |
| CTE Phase | Common Table Expressions (WITH clause); non-recursive and recursive |
| Phase 5 | ALTER TABLE, transactions |
| Phase 6 | Views, stored procedures, functions |
| Phase 7 | Statistics (CREATE STATISTICS, histograms, cardinality estimation) |

## User Stories

### Phase 1

- As a developer, I can create a database so that I have a writable directory for my data.
- As a developer, I can create tables with fixed-width columns so that I can define schema.
- As a developer, I can insert rows so that data is persisted in readable text files.
- As a developer, I can select rows (all or filtered) so that I can query my data.
- As a developer, I can update and delete rows so that I can modify my data.
- As a consumer, I can embed the engine via NuGet so that I need only a package reference and writable directory.

## Deployment Targets

- **CLI executable** — Standalone .exe (Windows) or binary (macOS, Linux)
- **NuGet API DLL** — Library for embedding in APIs, websites
- **Installable Service** — Windows Service, systemd, launchd (Phase 2)

## Constraints

- .NET 8 (or compatible)
- Cross-platform: Windows, macOS, Linux
- Lock coordinator for concurrent API calls
- No external database server
- Dual storage backends (text, binary); text default for learning
- Case-insensitive keywords; case-preserving identifiers

## Documentation

- Getting Started, Public API, CLI reference, and feature docs updated as code is built
- Sample Wiki database with schema, scripts, and CLI examples
