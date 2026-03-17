# ADR-002: Database Directory Structure

## Status

Accepted

## Context

The database needs a clear, extensible on-disk layout that supports tables, views, procedures, functions, and system metadata. The structure should work on Windows, macOS, and Linux.

## Decision

### Root and db/ Folder

- Root folder = database name (e.g., `DemoDb/`)
- `db/` folder contains all database-level properties (manifest, config)
- `db/` is the descriptor of the database

### Content Folders

- **Tables/** — One folder per table; each contains root data file, PK, FK, and index files
- **Views/** — Similar to Tables (late-project)
- **Procedures/** — One file per stored procedure
- **Functions/** — One file per user-defined function
- **~System/** — System-generated folder; same structure as Tables but for meta-information

### Table Folder File Naming

- `<TableName>.txt` — Root data file
- `<TableName>_PK.txt` — Primary key index
- `<TableName>_FK_<LinkedTable>.txt` — Foreign key index
- `<TableName>_INX_<Col1>_<Col2>_<N>.txt` — Index; N = increment when multiple indexes share same columns

### System Folder Prefix

Use `~` to prefix system-generated folders. Valid on all target platforms. Identifies engine-managed content.

## Consequences

### Positive

- Clear separation of database config, user content, and system content
- Extensible for views, procedures, functions
- Cross-platform compatible
- Consistent naming for indexes and keys

### Negative

- Migration needed from any prior layout (e.g., flat system/ folder)
