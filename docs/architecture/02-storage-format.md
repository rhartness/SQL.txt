# Storage Format

## Platform Support

SQL.txt is **cross-platform**: Windows, macOS, and Linux. File and folder names use characters valid on all platforms. The system folder prefix (`~`) is supported on all major operating systems.

## Directory Layout

The root folder is named after the database. All database content lives beneath it.

```
<DatabaseName>/                    # Root folder = database name
  db/                              # Database descriptor and properties
    manifest.json
    (other db-level config)
  Tables/                          # User tables
    <TableName>/
      <TableName>.txt              # Root data file
      <TableName>_PK.txt            # Primary key index
      <TableName>_FK_<LinkedTable>.txt   # Foreign key index (Phase 2+)
      <TableName>_INX_<Columns>_<N>.txt # Index (Phase 2+)
  Views/                           # Views (Phase 6)
    <ViewName>/
      ...
  Procedures/                      # Stored procedures (Phase 6)
    <ProcedureName>.txt
  Functions/                       # User-defined functions (Phase 6)
    <FunctionName>.txt
  ~System/                         # System-generated; meta-information
    (system tables, same structure as Tables/)
```

### System Folder Prefix

The `~` character prefixes system-generated folders (e.g., `~System`). This convention:

- Identifies folders that are managed by the engine, not user-created
- Works on Windows, macOS, and Linux (valid in file/folder names everywhere)
- Allows future system folders (e.g., `~Temp`, `~Cache`) to follow the same pattern

If platform constraints ever require an alternative, use a single ASCII character that is valid in filenames on all target systems (e.g., `_System`). The current specification uses `~`.

## db/ Folder

Contains all properties specific to the database itself. It describes the database.

### manifest.json

```json
{
  "engineVersion": "0.1.0",
  "storageFormatVersion": 1,
  "numberFormat": "standard",
  "textEncoding": "ascii",
  "defaultMaxShardSize": 20971520,
  "storageBackend": "text"
}
```

- **storageBackend** — Optional; `"text"` (human-readable) or `"binary"` (compact, performance). Default: `"text"`. Chosen at database creation; cannot be changed. Text uses `.txt` files; binary uses `.bin` for data files.

### Binary Data Format (storageBackend=binary)

When `storageBackend=binary`, table data files use extension `.bin` and a fixed-length record format:

- **Streaming:** Binary reads use `OpenReadStreamAsync` with chunked `ReadAsync` (64 KB buffer, ArrayPool); writes use `OpenWriteStreamAsync` for streaming without full-file materialization.
- **Record layout:** 1 byte flag (0=active, 1=deleted) + 8 bytes RowId (little-endian) + fixed-width column values per schema
- **Column bytes:** CHAR(n)=n, VARCHAR(n)=n, INT=4, TINYINT=1, BIGINT=8, BIT=1, DECIMAL=8
- **numberFormat** — Optional; default `"standard"` (decimal `.`). Override for locale-specific numeric formatting.
- **textEncoding** — Optional; UTF-8 supported for text backend. Fixed-width encodings (ASCII, Latin-1, UTF-16, UTF-32) also supported. Default: UTF-8 or platform default. UTF-8 is variable-length; line-delimited format still enables line-by-line streaming. See [06-durability-and-sharding.md](06-durability-and-sharding.md).
- **defaultMaxShardSize** — Optional; default 20 MB (20,971,520 bytes). Database-level default for table shard size. Overridable per table via `CREATE TABLE ... WITH (maxShardSize=...)`. See [06-durability-and-sharding.md](06-durability-and-sharding.md).

### Schema Location

- **~System/** — Master source of truth; engine always reads from here
- **Tables/\<TableName>/** — Reference copy only; for human inspection

## Tables/ Folder

One folder per table. Each table folder contains:

| File | Purpose |
|------|---------|
| `<TableName>.txt` | Root data file (rows) |
| `<TableName>_STOC.txt` | Shard Table of Contents — one line per shard (Phase 2+); see [adr-008](../decisions/adr-008-index-shard-structure.md) |
| `<TableName>_PK.txt` | Primary key index (Phase 2+); format: `Value\|ShardId\|_RowId` |
| `<TableName>_FK_<LinkedTable>.txt` | Foreign key index for FK to LinkedTable (Phase 2+) |
| `<TableName>_INX_<Col1>_<Col2>_<N>.txt` | Index on columns; N = increment if multiple indexes share same columns (Phase 2+); format: `Value\|ShardId\|_RowId` |

### Schema and Metadata

Schema and per-table metadata may be stored in the table folder or in `~System`. See versioning strategy below.

### Root Data File Format

**Format version 1 (fixed-width):** Positional rows with soft-delete marker:

```
A|1         Richard                                           richard@example.com
A|2         Alice                                             alice@example.com
D|3         Bob                                               bob@example.com
```

- `A`: Active row
- `D`: Deleted row
- Pipe-delimited; fields padded to fixed width per schema

**Format version 2 (variable-width):** Length-prefixed segments for tables with VARCHAR columns:

```
A|1|1:1|5:Hello|17:This is body text
```

- `A|` or `D|`: Active/deleted marker
- Each field: `length:value` (e.g., `5:Hello` = 5 chars, value "Hello")
- Values may contain `|` and `:`; length defines exact character count
- CHAR and VARCHAR columns stored without padding

## ~System/ Folder

Same structure as `Tables/` but for system tables. Stores meta-information about:

- User tables, columns, types
- Stored procedures, functions
- Indexes, keys, constraints

This is a mini-database that the engine can load in-memory to know how and where information is stored.

## Views/ Folder

Similar to Tables; each view has a folder. Views are stored queries over tables. (Phase 6.)

## Procedures/ and Functions/

One file per stored procedure or function. File name = object name. (Phase 6.)

## Sharding

Database-level **defaultMaxShardSize** (20 MB default) in manifest; per-table override via `MaxShardSize`. When table data file exceeds limit, create new shard: `<TableName>_1.txt`, `<TableName>_2.txt`, etc. Split strategy: stream half (or tail) to new shard; do not rewrite entire table. Indexes (Phase 2+) use `Value|ShardId|_RowId` format; STOC tracks shard ranges. See [06-durability-and-sharding.md](06-durability-and-sharding.md) and [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md).

## Versioning Strategy

- `FORMAT_VERSION` in schema enables future format evolution
- `storageFormatVersion` in manifest for cross-database compatibility
- Phase 1: fixed-width only; Phase 3+: variable-width (length-prefixed) is primary for tables with VARCHAR; fixed-width remains for legacy compatibility

## File Naming

- Table/view/procedure/function names map to folder/file names
- Case-preserving; normalized for lookup
- Index files: `<TableName>_INX_<ColumnNames>_<Increment>.txt` — column names in index order, underscore-separated; increment used when multiple indexes share the same column list
- Sharded data: `<TableName>_1.txt`, `<TableName>_2.txt`, etc.
