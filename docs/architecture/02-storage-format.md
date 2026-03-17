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
  Views/                           # Views (late-project feature)
    <ViewName>/
      ...
  Procedures/                      # Stored procedures (advanced SQL feature)
    <ProcedureName>.txt
  Functions/                       # User-defined functions (advanced SQL feature)
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
  "textEncoding": "ascii"
}
```

- **numberFormat** — Optional; default `"standard"` (decimal `.`). Override for locale-specific numeric formatting.
- **textEncoding** — Optional; only fixed-width encodings (each character = fixed bytes). No UTF-8. Default: ASCII or platform fixed-width.

### Schema Location

- **~System/** — Master source of truth; engine always reads from here
- **Tables/\<TableName>/** — Reference copy only; for human inspection

## Tables/ Folder

One folder per table. Each table folder contains:

| File | Purpose |
|------|---------|
| `<TableName>.txt` | Root data file (rows) |
| `<TableName>_PK.txt` | Primary key index (Phase 2+) |
| `<TableName>_FK_<LinkedTable>.txt` | Foreign key index for FK to LinkedTable (Phase 2+) |
| `<TableName>_INX_<Col1>_<Col2>_<N>.txt` | Index on columns; N = increment if multiple indexes share same columns (Phase 2+) |

### Schema and Metadata

Schema and per-table metadata may be stored in the table folder or in `~System`. See versioning strategy below.

### Root Data File Format

Fixed-width positional rows with soft-delete marker:

```
A|1         Richard                                           richard@example.com
A|2         Alice                                             alice@example.com
D|3         Bob                                               bob@example.com
```

- `A`: Active row
- `D`: Deleted row
- Pipe-delimited; fields padded to fixed width per schema

## ~System/ Folder

Same structure as `Tables/` but for system tables. Stores meta-information about:

- User tables, columns, types
- Stored procedures, functions
- Indexes, keys, constraints

This is a mini-database that the engine can load in-memory to know how and where information is stored.

## Views/ Folder

Similar to Tables; each view has a folder. Views are stored queries over tables. (Late-project feature.)

## Procedures/ and Functions/

One file per stored procedure or function. File name = object name. (Advanced SQL feature.)

## Sharding

Per-table **MaxShardSize** parameter. When table data file exceeds this, create new shard: `<TableName>_1.txt`, `<TableName>_2.txt`, etc. Indexes (Phase 2+) reference shard + position. Do not shard indexes initially. See [06-durability-and-sharding.md](06-durability-and-sharding.md).

## Versioning Strategy

- `FORMAT_VERSION` in schema enables future format evolution
- `storageFormatVersion` in manifest for cross-database compatibility
- Phase 1: fixed-width only; Phase 3: per-table format versions for VARCHAR

## File Naming

- Table/view/procedure/function names map to folder/file names
- Case-preserving; normalized for lookup
- Index files: `<TableName>_INX_<ColumnNames>_<Increment>.txt` — column names in index order, underscore-separated; increment used when multiple indexes share the same column list
- Sharded data: `<TableName>_1.txt`, `<TableName>_2.txt`, etc.
