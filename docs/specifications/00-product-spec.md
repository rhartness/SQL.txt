# Product Specification

## Purpose

**SQL.txt** is a lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in human-readable text files.

## Scope

### In Scope

- Human-readable on-disk storage
- Lightweight local embedded usage
- Phased implementation (Stage 0 → Phase 1 → Phase 2 → Phase 3)
- Single-table CRUD in Phase 1
- Fixed-width fields (CHAR) in Phase 1
- Indexes, PK/FK in Phase 2
- VARCHAR in Phase 3

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
| Phase 1 | CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE; fixed-width only |
| Phase 2 | Indexes, PK/FK, constraints, relational metadata |
| Phase 3 | VARCHAR, variable-width fields, storage evolution |

## User Stories

### Phase 1

- As a developer, I can create a database so that I have a writable directory for my data.
- As a developer, I can create tables with fixed-width columns so that I can define schema.
- As a developer, I can insert rows so that data is persisted in readable text files.
- As a developer, I can select rows (all or filtered) so that I can query my data.
- As a developer, I can update and delete rows so that I can modify my data.
- As a consumer, I can embed the engine via NuGet so that I need only a package reference and writable directory.

## Constraints

- .NET 8 (or compatible)
- Single-process friendly; basic file locking
- No external database server
- Human-readable formats only
- Case-insensitive keywords; case-preserving identifiers
