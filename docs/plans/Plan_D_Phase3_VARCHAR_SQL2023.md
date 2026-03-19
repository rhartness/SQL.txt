# Plan D: Phase 3 — VARCHAR and String Types (SQL:2023)

**Status:** Done  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan C complete (or Plan B if binary deferred)  
**Spec Reference:** ISO/IEC 9075-2:2023 SQL/Foundation — Clause 6 (Data types), 9 (Scalar expressions)

**Reference:** [docs/specifications/01_Initial_Creation.md](../specifications/01_Initial_Creation.md) Phase 3, [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)

---

## SQL:2023 Compliance

| Feature ID | Name | Status |
|------------|------|--------|
| Core | VARCHAR | Done |
| T055 | String padding (LPAD, RPAD) | Done |
| T056 | Multi-character TRIM | Done |
| T062 | CHAR_LENGTH, OCTET_LENGTH | Done |
| T081 | Optional VARCHAR max length | Done |
| T054 | GREATEST, LEAST | Enhancement |

**Full feature list:** See [01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md)

## Compliance Checklist

- [x] VARCHAR(n), VARCHAR (T081)
- [x] LPAD, RPAD (T055)
- [x] TRIM with multi-char (T056)
- [x] CHAR_LENGTH, OCTET_LENGTH (T062)
- [ ] T054 GREATEST, LEAST (optional enhancement)

---

## Scope

- VARCHAR, variable-width fields; storage evolution
- SQL:2023 features: T055 (LPAD, RPAD), T056 (multi-char TRIM), T062 (CHAR_LENGTH, OCTET_LENGTH), T081 (optional VARCHAR max)
- Enterprise-grade: streaming, copy-on-write, shard-aware
- **Both** text and binary backends must support VARCHAR

---

## Wave 1: Contracts and Schema

### 1.1 Add VARCHAR to ColumnType

- Extend ColumnType enum: `Varchar`
- ColumnDefinition: Width for VARCHAR max length; nullable for T081 (optional max)

### 1.2 Schema Format

- Extend schema serialization for VARCHAR
- Format version bump for variable-width support

---

## Wave 2: Row Format Evolution

### 2.1 Variable-Width Serialization (Text)

- Length-prefixed or delimiter-based for VARCHAR in text format
- Fixed-width columns remain fixed; VARCHAR uses variable bytes

### 2.2 Variable-Width Serialization (Binary)

- Length-prefixed (2-byte or 4-byte) for VARCHAR in binary format
- Backward compatible with existing fixed-width

---

## Wave 3: Parser

### 3.1 VARCHAR Syntax

- `VARCHAR(n)`, `VARCHAR` (T081: no explicit max)
- CREATE TABLE, ALTER TABLE ADD COLUMN

### 3.2 String Functions (T055, T056, T062)

- Parse `LPAD(str, len, pad)`, `RPAD(str, len, pad)`
- Parse `TRIM([LEADING|TRAILING|BOTH] [chars FROM] str)` — T056 multi-char
- Parse `CHAR_LENGTH(str)`, `OCTET_LENGTH(str)` — T062

---

## Wave 4: Engine — String Functions

### 4.1 Implement LPAD, RPAD (T055)

### 4.2 Implement TRIM with multi-char (T056)

### 4.3 Implement CHAR_LENGTH, OCTET_LENGTH (T062)

### 4.4 VARCHAR Storage

- TableDataStore: handle variable-width in both text and binary backends
- Index: VARCHAR keys — use normalized/key format for indexing

---

## Wave 5: Tests and Documentation

### 5.1 Unit Tests

- Parser: VARCHAR, LPAD, RPAD, TRIM, CHAR_LENGTH, OCTET_LENGTH
- Serialization: variable-width round-trip
- Engine: string functions

### 5.2 Documentation

- Update 11-sql2023-mapping.md: Phase 3 features marked done
- Update README Roadmap
- Update 02-storage-format.md for variable-width

---

## Plan Execution Rules (Apply on Completion)

1. Update README Roadmap table
2. Update 11-sql2023-mapping.md with T055, T056, T062, T081
3. Update CLI reference, Getting Started
4. Update 02-storage-format.md
5. Provide examples for CLI, WASM, Embedding

---

## Deliverable

VARCHAR support; T055, T056, T062, T081 implemented. Both text and binary backends support variable-width fields. Enterprise-grade streaming and shard-aware behavior.
