# Storage Format

## Directory Layout

```
/MyDatabase/
  db.manifest.json
  /system/
    tables.meta.txt
    engine.meta.json
  /tables/
    /Users/
      schema.txt
      data.txt
      table.meta.txt
```

## File Formats

### db.manifest.json

```json
{
  "engineVersion": "0.1.0",
  "storageFormatVersion": 1
}
```

### schema.txt (per table)

```
TABLE: Users
FORMAT_VERSION: 1
COLUMNS:
1|Id|CHAR|10
2|Name|CHAR|50
3|Email|CHAR|100
```

### table.meta.txt (per table)

```
TABLE: Users
ROW_COUNT: 12
ACTIVE_ROW_COUNT: 12
DELETED_ROW_COUNT: 0
LAST_UPDATED_UTC: 2026-03-16T00:00:00Z
```

### data.txt (per table)

Fixed-width positional rows with soft-delete marker:

```
A|1         Richard                                           richard@example.com
A|2         Alice                                             alice@example.com
D|3         Bob                                               bob@example.com
```

- `A`: Active row
- `D`: Deleted row
- Pipe-delimited; fields padded to fixed width per schema

### system/tables.meta.txt

```
Users|2026-03-16T00:00:00Z
Orders|2026-03-16T00:05:00Z
```

### system/columns.meta.txt (or equivalent)

```
Users|1|Id|CHAR|10
Users|2|Name|CHAR|50
Users|3|Email|CHAR|100
```

## Versioning Strategy

- `FORMAT_VERSION` in schema enables future format evolution
- `storageFormatVersion` in manifest for cross-table compatibility
- Phase 1: fixed-width only; Phase 3: per-table format versions for VARCHAR

## File Naming

- Table names map to folder/file names (e.g., `Users` → `tables/Users/`)
- Case-preserving; normalized for lookup
