# ADR-003: Phase 1 Design Decisions

## Status

Accepted

## Decisions

### 1. Semicolon

**Decision:** Semicolon is **optional** at end of statements.

### 2. CREATE DATABASE Path

**Decision:** Support **both** explicit path and relative path. When relative, use current working directory. Document explicitly in CLI and API docs.

### 3. Schema Location

**Decision:** Schema stored in **both** locations:
- **~System/** — Master source of truth; engine always reads from here
- **Tables/\<TableName>/** — Reference copy only; for human inspection; may be regenerated from ~System

### 4. Sample Wiki Verification

**Decision:** Phase 1 completion includes running `create-wiki.sql` and `seed-wiki.sql` as verification.

### 5. Data Types (Phase 1)

**Decision:** Support from the start:
- `CHAR(n)` — Fixed-width character
- `INT` — 32-bit integer
- `TINYINT` — 8-bit integer
- `BIGINT` — 64-bit integer
- `BIT` — Stored as `"1"` or `"0"` (not true/false)
- `DECIMAL(p,s)` — Stored as text equivalent; fixed width; pad with zeros

### 6. Number Format (CREATE DATABASE)

**Decision:** Optional parameter for numeric string formatting. Default: standard (English) format with decimal `.`. Allow override for other locales (e.g., comma as decimal separator).

### 7. Text Encoding

**Decision:** Only **fixed-width** encodings allowed. Each character must map to a fixed number of bytes. No UTF-8 or other variable-length encodings. Parameter at database creation.

### 8. Sharding

**Decision:** All data files (table data, metadata, indexes when added) must be **shardable** as they grow. Database-level `defaultMaxShardSize` (20 MB default) in manifest; per-table override: `MaxShardSize`. When a data file exceeds this, create new shard. Split strategy: stream half/tail to new shard; do not rewrite entire table. Indexes (Phase 2+) use `Value|ShardId|_RowId` format; STOC tracks shard ranges. Do not shard indexes initially; shard table data only. See [adr-007](adr-007-sharding-parameters.md) and [adr-008](adr-008-index-shard-structure.md).

### 8a. Efficiency (Knuth-Style)

**Decision:** Never implement the most straightforward approach for data-focused tasks. Design for speed and efficiency: choose algorithms (e.g., binary search, incremental updates) and structures (e.g., STOC for indexes) that scale well.

### 9. Testing

**Decision:** **Test-first** paradigm. Full unit test coverage per functional implementation. Test: high values, low values, multiple inputs, unexpected data, exception paths.

### 10. Error Handling (User-Edited Files)

**Decision:** When errors occur (e.g., corrupted files from manual edit): provide **file name, row number, character position** (or closest). Exception messages must explain how user interaction might have caused the issue. Enable easy inspection.
