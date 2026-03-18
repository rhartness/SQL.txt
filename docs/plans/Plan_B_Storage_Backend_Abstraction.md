# Plan B: Storage Backend Abstraction

**Status:** Pending  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan A complete

**Reference:** [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md), [docs/roadmap/00-sql2023-compliance-roadmap.md](../roadmap/00-sql2023-compliance-roadmap.md)

---

## Scope

Introduce `storageBackend: "text" | "binary"` at database creation. Text remains default; binary path is prepared but not implemented.

---

## Wave 1: Manifest and Contracts

### 1.1 Add storageBackend to Manifest

- In [src/SqlTxt.Storage/DatabaseCreator.cs](../../src/SqlTxt.Storage/DatabaseCreator.cs): Add `storageBackend` to manifest object (default: `"text"`)
- Add `GetStorageBackendAsync(databasePath)` to read `storageBackend` from manifest
- Manifest schema: `{ "storageBackend": "text" | "binary", ... }`

### 1.2 Extend CreateDatabaseCommand

- In [src/SqlTxt.Contracts/Commands/CreateDatabaseCommand.cs](../../src/SqlTxt.Contracts/Commands/CreateDatabaseCommand.cs): Add `StorageBackend?: string` (values: `"text"`, `"binary"`)

### 1.3 Add StorageBackend Type

- Add `StorageBackend` enum or sealed record in Contracts: `Text`, `Binary`
- Or use `string` with validation; document allowed values

**Acceptance:** CreateDatabaseCommand supports StorageBackend; manifest includes storageBackend.

---

## Wave 2: Parser

### 2.1 Parse CREATE DATABASE WITH (storageBackend=...)

- In [src/SqlTxt.Parser/SqlCommandParser.cs](../../src/SqlTxt.Parser/SqlCommandParser.cs): Extend WITH clause parsing for `storageBackend=text` or `storageBackend=binary`
- Invalid value throws ParseException
- Default: text when omitted

**Acceptance:** `CREATE DATABASE foo WITH (storageBackend=binary);` parses correctly.

---

## Wave 3: CLI

### 3.1 Add --storage Flag

- In [src/SqlTxt.Cli/Program.cs](../../src/SqlTxt.Cli/Program.cs): Add `--storage:text` or `--storage:binary` to create-db command
- Default: `--storage:text`
- Pass to CreateDatabaseCommand when creating via CLI

**Acceptance:** `sqltxt create-db MyDb --storage:binary` creates database with storageBackend=binary.

---

## Wave 4: IStorageBackend Interface

### 4.1 Define IStorageBackend

- Create `IStorageBackend` in Contracts (or Storage):
  - `string DataFileExtension` — `.txt` or `.bin`
  - `string IndexFileExtension` — `.txt` or `.idx`
  - `string SchemaFileExtension` — `.txt` or `.bin`
  - `bool IsTextBackend` — for branching in stores

### 4.2 Implement TextStorageBackend

- `TextStorageBackend`: returns `.txt` for all; `IsTextBackend = true`

### 4.3 Implement BinaryStorageBackend (Stub)

- `BinaryStorageBackend`: returns `.bin`, `.idx`; `IsTextBackend = false`
- No binary I/O yet; just returns extensions for future use

### 4.4 StorageBackendFactory

- `GetStorageBackend(string storageBackend)` — returns TextStorageBackend or BinaryStorageBackend from manifest value

**Acceptance:** IStorageBackend exists; TextStorageBackend and BinaryStorageBackend (stub) implemented.

---

## Wave 5: Wire into Engine

### 5.1 DatabaseCreator.CreateDatabase

- Accept `storageBackend` parameter; write to manifest

### 5.2 Engine Composition

- When opening database: read manifest `storageBackend`; resolve IStorageBackend
- Pass IStorageBackend to TableDataStore, SchemaStore (or store in a context that stores can access)
- For now: TableDataStore continues using .txt paths; SchemaStore unchanged
- Goal: Backend is available for Plan C; no behavior change yet

**Acceptance:** Manifest written with storageBackend; engine can resolve IStorageBackend from manifest.

---

## Wave 6: Documentation

### 6.1 Update Docs

- [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md): Document storageBackend in manifest; file extensions per backend
- [docs/cli-reference.md](../cli-reference.md): Document `--storage:text` and `--storage:binary`
- [README.md](../../README.md): Update Roadmap row for "Storage Abstraction" to In Progress or Done

**Acceptance:** Docs reflect new storage backend option.

---

## Plan Execution Rules (Apply on Completion)

1. Update README Roadmap table
2. Update 11-sql2023-mapping.md if applicable
3. Update CLI reference, Getting Started
4. Update 02-storage-format.md
5. Provide examples for CLI, WASM, Embedding

---

## Deliverable

Text remains default; binary path prepared. `--storage:binary` creates database with storageBackend=binary in manifest; binary I/O not yet implemented (Plan C).
