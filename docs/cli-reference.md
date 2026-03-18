# SQL.txt CLI Reference

## Options

### --wasm

Use WASM-compatible in-memory storage, persisted to a `.wasmdb` file. When specified, the database is stored in a single JSON file instead of a directory of text files. This mode simulates the storage backend that would be used when running in a WebAssembly browser environment.

- For `create-db`: path becomes the persistence file (e.g., `./MyDb` creates `./MyDb.wasmdb`)
- For other commands: `--db` must point to the `.wasmdb` file (e.g., `--db ./MyDb.wasmdb --wasm`)

## Commands

### create-db

Creates a new database at the specified path.

```
sqltxt create-db <path> [--wasm] [--storage:text|binary]
```

**Options:**
- `--storage:text` — Human-readable files (default)
- `--storage:binary` — Compact binary files for performance

**Examples:**
```bash
sqltxt create-db ./WikiDb
sqltxt create-db ./WikiDb --wasm
sqltxt create-db ./WikiDb --storage:binary
sqltxt create-db ./WikiDb --storage:text
```

### exec

Executes a single SQL statement (CREATE, INSERT, UPDATE, DELETE).

```
sqltxt exec --db <path> [--wasm] "<statement>"
```

**Examples:**
```bash
sqltxt exec --db ./WikiDb "CREATE TABLE User (Id CHAR(10), Name CHAR(50))"
sqltxt exec --db ./WikiDb.wasmdb --wasm "INSERT INTO User (Id, Name) VALUES ('1', 'Alice')"
```

### query

Executes a SELECT query and prints the result grid.

```
sqltxt query --db <path> [--wasm] "<select>"
```

**Examples:**
```bash
sqltxt query --db ./WikiDb "SELECT * FROM User"
sqltxt query --db ./WikiDb.wasmdb --wasm "SELECT * FROM User"
sqltxt query --db ./WikiDb "SELECT Id, Name FROM User WHERE Id = '1'"
```

### script

Executes a SQL script file (semicolon-separated statements).

```
sqltxt script --db <path> [--wasm] <file>
```

**Examples:**
```bash
sqltxt script --db ./WikiDb docs/samples/wiki-database/create-wiki.sql
sqltxt script --db ./WikiDb.wasmdb --wasm docs/samples/wiki-database/seed-wiki.sql
```

### inspect

Lists tables, columns, and row counts.

```
sqltxt inspect --db <path> [--wasm]
```

**Examples:**
```bash
sqltxt inspect --db ./WikiDb
sqltxt inspect --db ./WikiDb.wasmdb --wasm
```

### rebalance

Rebalances shards for a table. Compacts soft-deleted rows and redistributes data across shards according to `MaxShardSize`. Use after many deletes to reclaim space.

```
sqltxt rebalance --db <path> --table <name> [--wasm]
```

**Examples:**
```bash
sqltxt rebalance --db ./WikiDb --table Page
```

### WITH (NOLOCK)

For `query` and `exec` with SELECT: add `WITH (NOLOCK)` to skip read locks (dirty reads). Useful when you need to read without blocking writers.

```bash
sqltxt query --db ./WikiDb "SELECT * FROM User WITH (NOLOCK)"
```

## Common Workflows

### Create and seed the sample Wiki database

**Filesystem:**
```bash
sqltxt create-db ./WikiDb
sqltxt script --db ./WikiDb docs/samples/wiki-database/create-wiki.sql
sqltxt script --db ./WikiDb docs/samples/wiki-database/seed-wiki.sql
sqltxt query --db ./WikiDb "SELECT * FROM Page"
```

**WASM:**
```bash
sqltxt create-db ./WikiDb --wasm
sqltxt script --db ./WikiDb.wasmdb --wasm docs/samples/wiki-database/create-wiki.sql
sqltxt script --db ./WikiDb.wasmdb --wasm docs/samples/wiki-database/seed-wiki.sql
sqltxt query --db ./WikiDb.wasmdb --wasm "SELECT * FROM Page"
```

### build-sample-wiki (quick setup)

**Filesystem:**
```bash
sqltxt build-sample-wiki --db .
sqltxt query --db ./WikiDb "SELECT * FROM User"
```

**WASM:**
```bash
sqltxt build-sample-wiki --db . --wasm
sqltxt query --db ./WikiDb.wasmdb --wasm "SELECT * FROM User"
```

### WASM mode (browser-compatible storage)

```bash
sqltxt create-db ./MyDb --wasm
sqltxt exec --db ./MyDb.wasmdb --wasm "CREATE TABLE User (Id CHAR(10), Name CHAR(50))"
sqltxt exec --db ./MyDb.wasmdb --wasm "INSERT INTO User (Id, Name) VALUES ('1', 'Alice')"
sqltxt query --db ./MyDb.wasmdb --wasm "SELECT * FROM User"
```

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Usage error, invalid arguments |
| 2 | Parse error |
| 3 | Schema error (table/column not found) |
| 4 | Validation error (e.g., value too long) |
| 5 | Storage error (file I/O, database not found) |
| 99 | Unexpected error |
