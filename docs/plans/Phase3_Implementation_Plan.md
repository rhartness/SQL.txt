# Phase 3 Implementation Plan

This plan implements **Phase 3**: VARCHAR, variable-width fields, and storage evolution.

**Status:** Done

**Reference:** [docs/specifications/01_Initial_Creation.md](../specifications/01_Initial_Creation.md) (Phase 3 section, lines 702-772)  
**Storage format:** [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md) (line 131: per-table format versions)  
**SQL:2023 mapping:** [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md) (Phase 3: VARCHAR, T055, T056, T062, T081)  
**Efficiency:** [docs/architecture/10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md)

---

## Prerequisites

- Phase 1 and Phase 2 complete
- .NET 8 SDK
- Cross-platform: Windows, macOS, Linux

---

## Resolved Decisions

- **Storage format:** Length-prefixed readable records per spec recommendation (Option B); schema-aware delimited with escaping as fallback
- **Per-table format version:** FORMAT_VERSION 1 = fixed-width only (Phase 1/2); FORMAT_VERSION 2 = mixed CHAR + VARCHAR
- **Compatibility:** Phase 1/2 fixed-width tables remain unchanged; no migration of existing data
- **VARCHAR max length:** Enforced at insert/update; truncate with warning or reject per validation policy

---

## Wave 1: Contracts and Schema Extensions

### 1.1 Add ColumnType.VarChar

- [x] Add `ColumnType.VarChar` to [ColumnType.cs](../../src/SqlTxt.Contracts/ColumnType.cs)

### 1.2 Extend ColumnDefinition

- [x] Extend `ColumnDefinition`: `StorageWidth` for VARCHAR returns max length; add `IsVariableWidth` or derive from Type

### 1.3 Define Row Format Version

- [x] Define `RowFormatVersion` enum or constant: 1 = fixed-width, 2 = mixed (variable-width capable)

### 1.4 Extend Schema Format for VARCHAR

- [x] Extend schema format to persist `VARCHAR|n` (e.g., `4|Title|VARCHAR|100` in COLUMNS section)

### 1.5 Add FormatVersion to Table Schema

- [x] Add `FormatVersion` to table schema metadata (FORMAT_VERSION: 1 or 2)

**Acceptance:** Contracts compile; schema format supports VARCHAR; FORMAT_VERSION stored per table.

---

## Wave 2: Parser Extensions

### 2.1 Parse VARCHAR(n)

- [x] Parse `VARCHAR(n)` in CREATE TABLE column definitions

### 2.2 Validate VARCHAR Width

- [x] Validate n > 0 for VARCHAR

### 2.3 Support Mixed CHAR and VARCHAR

- [x] Support mixed CHAR and VARCHAR in same table: `CREATE TABLE Notes (Id CHAR(10), Title VARCHAR(100), Body VARCHAR(1000));`

**Acceptance:** Parser returns ColumnDefinition with Type=VarChar and Width=n; invalid syntax throws ParseException.

---

## Wave 3: Storage — Variable-Width Serialization

### 3.1 Implement Length-Prefixed Format

- [x] Implement length-prefixed format per spec (Option B): e.g., `A|_RowId|col1[L1]=val1|col2[L2]=val2` or `A|_RowId|5:Hello|17:This is body text` (length-prefixed segments)

### 3.2 Define Escaping Rules

- [x] Define escaping rules for delimiter collision (pipe, equals) within values; reuse/extend FieldCodec for VARCHAR

### 3.3 Create VariableWidthRowSerializer and VariableWidthRowDeserializer

- [x] Create `VariableWidthRowSerializer` and `VariableWidthRowDeserializer` implementing IRowSerializer/IRowDeserializer

### 3.4 CHAR Columns in Format 2

- [x] CHAR columns in format 2: store as variable-length (no padding) or retain fixed-width for consistency; spec favors variable for VARCHAR tables

### 3.5 Efficiency

- [x] **Efficiency:** Streaming reads/writes; no full-table load for format selection

**Acceptance:** Rows with VARCHAR serialize/deserialize correctly; format is human-readable; delimiter escaping works.

---

## Wave 4: Storage — Format Version and Selection

### 4.1 Schema Store FORMAT_VERSION

- [x] Schema store reads/writes FORMAT_VERSION (1 or 2)

### 4.2 Tables with VARCHAR Use Format 2

- [x] Tables with any VARCHAR column use FORMAT_VERSION 2

### 4.3 Engine Selects Serializer by FormatVersion

- [x] Engine/TableDataStore selects serializer by FormatVersion: 1 → FixedWidth, 2 → VariableWidth

### 4.4 Backward Compatibility

- [x] Backward compatibility: existing Phase 1/2 schemas without FORMAT_VERSION default to 1

**Acceptance:** Fixed-width tables load unchanged; new VARCHAR tables use format 2; no migration of existing data.

---

## Wave 5: Engine — CREATE TABLE with VARCHAR

### 5.1 Execute CREATE TABLE with VARCHAR

- [x] Execute CREATE TABLE with VARCHAR columns; write schema with FORMAT_VERSION 2

### 5.2 Create Table Folder and Data File

- [x] Create table folder and initial data file (empty or header as needed)

### 5.3 Index Files Unchanged

- [x] Index files (PK, FK, secondary) unchanged; index values are strings; VARCHAR values work identically

**Acceptance:** CREATE TABLE with VARCHAR succeeds; schema and folder created; indexes created if PK/FK/UNIQUE.

---

## Wave 6: Engine — INSERT, UPDATE, DELETE, SELECT with VARCHAR

### 6.1 INSERT Validation

- [x] INSERT: validate VARCHAR length (truncate with warning or reject per policy); use VariableWidthRowSerializer

### 6.2 UPDATE Validation

- [x] UPDATE: same validation for VARCHAR columns

### 6.3 DELETE with Variable-Width

- [x] DELETE: soft-delete works with variable-width format

### 6.4 SELECT with Variable-Width

- [x] SELECT: deserialize with VariableWidthRowDeserializer; project correctly

### 6.5 Index Maintenance

- [x] Index maintenance: INSERT/UPDATE/DELETE update indexes; VARCHAR values in index format unchanged (Value|ShardId|_RowId)

**Acceptance:** Full CRUD works on tables with VARCHAR; indexes stay consistent; length validation enforced.

---

## Wave 7: Sharding and Rebalance with Variable-Width

### 7.1 Shard Split

- [x] Shard split: variable-width rows stream correctly to new shard

### 7.2 RebalanceTableAsync

- [x] RebalanceTableAsync: handles format 2 tables; STOC and indexes updated

### 7.3 Efficiency

- [x] **Efficiency:** Streaming; no full load; atomic writes

**Acceptance:** VARCHAR tables shard and rebalance correctly; STOC and indexes updated.

---

## Wave 8: Tests

### 8.1 Unit Tests

- [x] Unit: Parser VARCHAR parsing; schema read/write with VARCHAR; VariableWidthRowSerializer/Deserializer

### 8.2 Integration Tests

- [x] Integration: CREATE TABLE with VARCHAR; INSERT/SELECT/UPDATE/DELETE; mixed CHAR/VARCHAR; index consistency

### 8.3 Golden File Tests

- [ ] Golden file: expected row format for sample VARCHAR table (optional; not shipped—covered by serializer/unit tests)

**Acceptance:** All tests pass; coverage for new paths.

---

## Wave 9: Hardening and Documentation

### 9.1 Error Messages

- [x] Error messages: clear validation errors for VARCHAR length; StorageException with file/row/position

### 9.2 Sample Wiki Update

- [x] Sample Wiki: add VARCHAR columns (e.g., PageContent.Content VARCHAR(5000)); rebuild sample

### 9.3 Docs Update

- [x] Docs: Getting Started, CLI Reference, 02-storage-format.md (format version 2), 11-sql2023-mapping.md

### 9.4 Examples

- [x] Examples: CLI (filesystem), CLI (WASM), Embedding per documentation standards

**Acceptance:** Docs current; sample Wiki uses VARCHAR; examples for all implementation types.

---

## Phase 3 Complete Checklist

- [x] All waves 1–9 complete (8.3 golden file deferred/optional)
- [x] `dotnet build` succeeds
- [x] `dotnet test` passes
- [x] Sample Wiki with VARCHAR builds and runs (`CreateWiki.sql` uses `VARCHAR`)
- [x] README and docs updated
- [x] Plan execution rules: README Roadmap, 11-sql2023-mapping, docs/cli-reference, 02-storage-format

---

## Step 10: After Phase 3 — Phase 3.5, then Phase 4

When Phase 3 is complete, **Phase 3.5** (storage and ingest efficiency) runs next; see [Phase3_5_Storage_Efficiency_Plan.md](Phase3_5_Storage_Efficiency_Plan.md).

For **Phase 4** (JOINs, aggregates, `ORDER BY`, `GROUP BY`, subqueries), authoritative scope and SQL:2023 mapping live in:

- [docs/specifications/02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md)
- [docs/roadmap/00-sql2023-compliance-roadmap.md](../roadmap/00-sql2023-compliance-roadmap.md)
- [docs/roadmap/01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md)

When starting Phase 4 implementation, add a new phase plan under `docs/plans/` if you want checkbox-style tracking; there is no separate archived `Phase4_Implementation_Plan.md` in the repo today.

---

## Diagram: Phase 3 Data Flow

```mermaid
flowchart TB
    subgraph CreateTable [CREATE TABLE with VARCHAR]
        CMD[CreateTableCommand]
        CMD --> HASVARCHAR{Any VARCHAR col?}
        HASVARCHAR -->|yes| FV2[FORMAT_VERSION 2]
        HASVARCHAR -->|no| FV1[FORMAT_VERSION 1]
        FV2 --> SCHEMA[Write schema]
        FV1 --> SCHEMA
    end

    subgraph Serialize [Row Serialization]
        ROW[RowData]
        ROW --> FMT{FormatVersion}
        FMT -->|1| FIXED[FixedWidthRowSerializer]
        FMT -->|2| VAR[VariableWidthRowSerializer]
        FIXED --> OUT1["A|_RowId|padded..."]
        VAR --> OUT2["A|_RowId|len:val|..."]
    end

    subgraph Index [Index Unchanged]
        IDX[Value|ShardId|_RowId]
    end
```

---

## Efficiency Notes

- Variable-width format: length-prefixed avoids escaping complexity for common cases; use minimal escaping only for reserved chars in values
- Serializer selection: O(1) per table at load; no runtime format detection per row
- Sharding: same streaming strategy as Phase 2; variable-length rows do not affect shard split logic
