# SQLTxt Phase 1 — Core DML/DDL

This plan implements the **Phase 1** minimal readable database engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE with fixed-width types (CHAR, INT, TINYINT, BIGINT, BIT, DECIMAL).

**Status:** Done  
**Prerequisites:** Stage 0 complete  
**Spec Reference:** ISO/IEC 9075-2:2023 SQL/Foundation — Clause 4 (Schema definition), 14 (Data manipulation)

**Reference:** [docs/specifications/01_Initial_Creation.md](../specifications/01_Initial_Creation.md) (Phase 1 section)  
**Cursor prompts:** [docs/prompts/phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)  
**Storage format:** [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md)  
**SQL:2023 mapping:** [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)  
**Feature registry:** [docs/roadmap/01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md)  
**Design decisions:** [docs/decisions/adr-003-phase1-design-decisions.md](../decisions/adr-003-phase1-design-decisions.md)  
**Durability/sharding:** [docs/architecture/06-durability-and-sharding.md](../architecture/06-durability-and-sharding.md)  
**API/deployment:** [docs/architecture/07-api-and-deployment.md](../architecture/07-api-and-deployment.md)  
**Concurrency:** [docs/architecture/08-concurrency-and-locking.md](../architecture/08-concurrency-and-locking.md)

---

## SQL:2023 Compliance

| Feature | SQL:2023 Basis | Status |
|---------|----------------|--------|
| CREATE TABLE | Core schema definition | Done |
| INSERT | Insert statement | Done |
| SELECT | Query specification | Done |
| UPDATE | Update statement | Done |
| DELETE | Delete statement | Done |
| Data types | CHAR, INT, TINYINT, BIGINT, BIT, DECIMAL | Done |

**Full feature list:** See [01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md)

## CREATE TABLE Compliance Checklist

- [x] CREATE TABLE name (column_def, ...)
- [x] Column types: CHAR(n), INT, TINYINT, BIGINT, BIT, DECIMAL(p,s)
- [x] Column order preserved
- [x] Table names unique; column names unique
- [ ] NOT NULL (Phase 1+ enhancement)
- [ ] DEFAULT expression (Phase 5 with ALTER)

---

## Prerequisites

- Stage 0 complete (solution, projects, docs, Cursor rules)
- .NET 8 SDK
- Cross-platform: Windows, macOS, Linux

---

## Resolved Decisions (ADR-003)

- Semicolon optional
- Path: support both explicit and relative (relative = current working directory)
- Schema: ~System = master; table folder = reference copy
- Sample Wiki: run create-wiki.sql and seed-wiki.sql as Phase 1 verification
- Data types: CHAR, INT, TINYINT, BIGINT, BIT, DECIMAL
- NumberFormat and TextEncoding parameters at CREATE DATABASE
- TextEncoding: UTF-8 supported; fixed-width optional (adr-003)
- Sharding: per-table MaxShardSize; database default defaultMaxShardSize (20 MB) per [adr-007](../decisions/adr-007-sharding-parameters.md); shard data files when too large
- Efficiency: Knuth-style — design for speed and efficiency; avoid straightforward-but-slow approaches
- SQL:2023: Phase 1 implements applicable subset per [11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)
- Test-first; full unit coverage
- Error handling: file name, row number, character position

---

## Wave 1: Solution Foundation (Verify)

Stage 0 created the scaffolding. Verify and fix if needed.

- [x] **1.1** Verify solution builds: `dotnet build`
- [x] **1.2** Verify all test projects run: `dotnet test`
- [x] **1.3** Verify project references match layered architecture (Engine → Contracts, Core, Storage, Parser; CLI → Engine)
- [x] **1.4** Remove placeholder `PlaceholderTests.cs` only when real tests replace them (do not remove until Wave 8)

**Acceptance:** Solution builds with 0 errors; all placeholder tests pass.

---

## Wave 2: Contracts

Implement shared models and interfaces in SqlTxt.Contracts.

- [x] **2.1** Implement `TableDefinition`, `ColumnDefinition`, `ColumnType` (CHAR, INT, TINYINT, BIGINT, BIT, DECIMAL)
- [x] **2.2** Implement `RowData` or equivalent row abstraction
- [x] **2.3** Implement `QueryResult`, `EngineResult` for execution output
- [x] **2.4** Implement command objects: `CreateDatabaseCommand` (with optional NumberFormat, TextEncoding), `CreateTableCommand`, `InsertCommand`, `SelectCommand`, `UpdateCommand`, `DeleteCommand`
- [x] **2.5** Implement exception hierarchy: `SqlTxtException`, `ParseException`, `SchemaException`, `ValidationException`, `StorageException`, `ConstraintViolationException` — all include file name, row, position when applicable
- [x] **2.6** Define core interfaces: `IDatabaseEngine` (async: `ExecuteAsync`, `ExecuteQueryAsync`, `OpenAsync`), `ICommandParser`, `ISchemaStore`, `ITableDataStore`, `IMetadataStore`, `IFileSystemAccessor`, `IRowSerializer`, `IRowDeserializer`, `IDatabaseLockManager` (Phase 1: simple mutex)
- [x] **2.7** Add `MaxShardSize` to table definition (per-table sharding parameter)

**Acceptance:** Contracts project compiles; types are immutable where reasonable; interfaces are usable by dependent projects.

**Cursor prompt:** Wave 2 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 3: Storage Foundation

Implement filesystem-backed storage per the db/, Tables/, ~System/ layout.

- [x] **3.1** Implement `IFileSystemAccessor` abstraction for file I/O (enables testability)
- [x] **3.2** Create database root folder (database name) and `db/` folder with `manifest.json` (include numberFormat, textEncoding)
- [x] **3.3** Create `Tables/` with one folder per table; each table folder contains `<TableName>.txt` (root data file)
- [x] **3.4** Create `~System/` folder for system metadata (tables, columns)
- [x] **3.5** Implement schema persistence: write schema to **both** ~System (master) and table folder (reference copy); read always from ~System
- [x] **3.6** Implement table metadata: ROW_COUNT, ACTIVE_ROW_COUNT, DELETED_ROW_COUNT, LAST_UPDATED_UTC
- [x] **3.7** Implement row serialization: fixed-width positional format with soft-delete marker (A| or D|); BIT as "1"/"0"; DECIMAL padded with zeros
- [x] **3.8** Ensure cross-platform paths (Windows, macOS, Linux)
- [x] **3.9** Add sharding support: when table data exceeds MaxShardSize, create new shard file

**Acceptance:** Creating a database produces correct directory structure; creating a table produces table folder and schema in both locations; inserting a row appends correctly formatted data.

**Cursor prompt:** Wave 3 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 4: Parser v1

Implement tokenizer and parser for Phase 1 SQL subset.

- [x] **4.1** Implement tokenizer: keywords, identifiers, literals (single-quoted strings), punctuation
- [x] **4.2** Parse `CREATE DATABASE <name> [WITH (numberFormat=..., textEncoding=...)];` — semicolon optional
- [x] **4.3** Parse `CREATE TABLE <name> (col1 type, col2 type, ...);` with CHAR(n), INT, TINYINT, BIGINT, BIT, DECIMAL(p,s)
- [x] **4.4** Parse `INSERT INTO <table> (cols) VALUES (vals);`
- [x] **4.5** Parse `SELECT <cols|*> FROM <table> [WHERE col = 'literal'];`
- [x] **4.6** Parse `UPDATE <table> SET col = 'literal' [, ...] [WHERE col = 'literal'];`
- [x] **4.7** Parse `DELETE FROM <table> [WHERE col = 'literal'];`
- [x] **4.8** Return strongly typed command objects from `ICommandParser`
- [x] **4.9** Report parse errors with line/column when possible via `ParseException`

**Acceptance:** Parser returns correct command type for each statement; invalid syntax throws ParseException; WHERE supports equality-only.

**Cursor prompt:** Wave 4 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 5: Core Engine v1

Implement execution for CREATE DATABASE, CREATE TABLE, INSERT, SELECT.

- [x] **5.1** Implement `IDatabaseEngine` (or equivalent) as main entry point with async methods (`ExecuteAsync`, `ExecuteQueryAsync`, `OpenAsync`); support both explicit and relative paths (relative = current working directory)
- [x] **5.2** Implement basic locking: `IDatabaseLockManager` with single mutex per database; acquire before write, release after
- [x] **5.3** Execute `CREATE DATABASE`: create root folder, db/, manifest (with numberFormat, textEncoding), Tables/, ~System/
- [x] **5.4** Execute `CREATE TABLE`: create table folder, schema, metadata; register in ~System
- [x] **5.5** Execute `INSERT`: validate schema, columns, widths; append row to `<TableName>.txt`; update metadata
- [x] **5.6** Execute `SELECT`: read schema, scan data file, skip deleted rows, apply optional equality predicate, return projected columns
- [x] **5.7** Validate column widths before insert; reject values longer than defined width
- [x] **5.8** Use full table scan (no indexes in Phase 1)

**Acceptance:** End-to-end: create db, create table, insert rows, select all, select with WHERE — all persist and return correct data.

**Cursor prompt:** Wave 5 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 6: Update/Delete

Extend engine for UPDATE and DELETE.

- [x] **6.1** Execute `UPDATE`: scan rows, match WHERE, rewrite/update row, enforce width validation
- [x] **6.2** Execute `DELETE`: scan rows, match WHERE, mark as deleted (D|) — soft delete
- [x] **6.3** Update metadata after insert, update, delete: ROW_COUNT, ACTIVE_ROW_COUNT, DELETED_ROW_COUNT, LAST_UPDATED_UTC
- [x] **6.4** SELECT skips rows with D| marker

**Acceptance:** UPDATE modifies matching rows; DELETE marks rows deleted; SELECT excludes deleted rows; metadata counts are correct.

**Cursor prompt:** Wave 6 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 6.5: NuGet Packaging

- [x] **6.5.1** Add NuGet package project (SqlTxt.csproj) that packages Engine + Contracts, Core, Storage, Parser
- [x] **6.5.2** Configure package metadata (id: SqlTxt, version, description)
- [x] **6.5.3** Ensure CLI and SampleApp consume Engine via project reference (same as NuGet consumers would)

**Acceptance:** `dotnet pack` produces SqlTxt.nupkg; package can be referenced by external projects.

---

## Wave 7: CLI / Consumer App

Implement CLI and update SampleApp.

- [x] **7.1** CLI: `create-db <path>` — create database
- [x] **7.2** CLI: `exec --db <path> "<statement>"` — execute single statement
- [x] **7.3** CLI: `query --db <path> "<select>"` — execute SELECT and print result grid
- [x] **7.4** CLI: `script --db <path> <file>` — execute script file
- [x] **7.5** CLI: `inspect --db <path>` — print tables, columns, row counts (optional)
- [x] **7.6** SampleApp: create database, create table, insert, select — demonstrate embedding

**Acceptance:** CLI can create db, run statements, run scripts; SampleApp demonstrates full flow.

**Cursor prompt:** Wave 7 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 8: Tests

**Test-first paradigm.** Full unit test coverage per functional implementation.

- [x] **8.1** Parser unit tests: tokenization, each statement type, error cases; high/low values, multiple inputs, unexpected data, exception paths
- [x] **8.2** Storage unit tests: schema serialization, row format, metadata (mock IFileSystemAccessor); all data types; edge cases
- [x] **8.3** Engine unit tests: validation, command execution (mocked storage); exception paths
- [x] **8.4** Integration tests: create temp db, create table, insert, select, update, delete, verify file contents
- [ ] **8.5** Golden file tests (optional): known schema/data input → expected file output
- [x] **8.6** Replace or remove PlaceholderTests with real tests
- [x] **8.7** Test corrupted file handling: verify exceptions include file name, row number, character position

**Acceptance:** All tests pass; integration tests prove persisted text format matches spec; full coverage per function.

**Cursor prompt:** Wave 8 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 9: Hardening

Improve robustness and error handling.

- [x] **9.1** Corruption handling: detect malformed schema, invalid metadata; when errors occur, provide file name, row number, character position (or closest)
- [x] **9.2** Exception messages: explain how user interaction (e.g., manual file edit) might have caused the issue
- [x] **9.3** Basic file locking on writes; Phase 1 uses `IDatabaseLockManager` (single-writer mutex); Phase 2 will add multi-reader + NOLOCK
- [x] **9.4** Clearer parser error messages (line, column, expected vs found)
- [x] **9.5** Expand test coverage for edge cases (empty table, max width, invalid paths)
- [x] **9.6** Update docs: Getting Started, CLI Reference, API docs per [05-documentation-standards.md](../architecture/05-documentation-standards.md)

**Acceptance:** Malformed input handled gracefully; parser errors are actionable; docs are current.

**Cursor prompt:** Wave 9 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 10: Sample Wiki Verification

- [x] **10.0** Delete existing `./WikiDb` if present (rebuild from scratch); add WikiDb to .gitignore
- [x] **10.1** Implement `BuildSampleWikiAsync` API (Engine, CLI, NuGet, Service)
- [x] **10.2** Run `sqltxt build-sample-wiki --db .` (or manual create-db + script)
- [x] **10.3** Verify `sqltxt query --db ./WikiDb "SELECT * FROM Page;"` returns expected rows
- [x] **10.4** Verify `sqltxt query --db ./WikiDb "SELECT Id, PageId, Content FROM PageContent;"` returns rows with decoded newlines/tabs
- [x] **10.5** Rebuild sample database when new features are added; verify new behavior in generated files

**Acceptance:** Sample Wiki database creates and seeds successfully; BuildSampleWiki is the canonical build method; queries return correct data; CHAR encoding round-trips correctly.

---

## Phase 1 Complete Checklist

- [x] All waves 1–10 complete (including Wave 6.5 NuGet packaging)
- [x] `dotnet build` succeeds
- [x] `dotnet test` passes
- [x] CLI can create db, run create-wiki.sql, run seed-wiki.sql
- [x] SampleApp runs successfully
- [x] README and docs updated

**Phase 1 is complete.** (Wave 8.5 golden file tests are optional and deferred.)

---

## Back-Plan: Strategic Updates (Post-Phase 1)

Per ADR-007, ADR-008, and strategic updates:

- [x] **BP.1** Add `defaultMaxShardSize` (20 MB) to manifest schema; DatabaseCreator writes it
- [x] **BP.2** SchemaStore: when creating table, use `table.MaxShardSize ?? db.DefaultMaxShardSize` (Engine resolves; schema persists)
- [x] **BP.3** Parser: parse `CREATE DATABASE ... WITH (defaultMaxShardSize=...)`; `CREATE TABLE ... WITH (maxShardSize=...)`
- [x] **BP.4** CreateDatabaseCommand: add `DefaultMaxShardSize` property

---

## Step 11: Phase 2 Plan Prompt

- [x] **11.1** Phase 2 plan created at [`Phase2_Implementation_Plan.md`](Phase2_Implementation_Plan.md). Original prompt (historical):

> **Prompt:** Using the plan at `docs/plans/Phase1_Implementation_Plan.md` and the specs in `docs/specifications/01_Initial_Creation.md` (Phase 2 section), create a new Phase 2 implementation plan.
>
> The plan should:
> - Be saved to `docs/plans/Phase2_Implementation_Plan.md`
> - Follow the same format: each step as a referencable item with a checkbox
> - Cover: Primary keys, foreign keys, indexes, constraint enforcement, metadata expansion; full lock manager (read/write); WITH (NOLOCK); SqlTxt.Service project
> - Include concrete acceptance criteria per wave
> - Reference Phase 2 requirements from the spec
> - End with a step to prompt for the Phase 3 plan when Phase 2 is complete

---

