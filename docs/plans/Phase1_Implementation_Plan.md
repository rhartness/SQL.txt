# SQLTxt Phase 1 — Implementation Plan

This plan implements the **Phase 1** minimal readable database engine: CREATE DATABASE/TABLE, INSERT, SELECT, UPDATE, DELETE with fixed-width CHAR(n) storage.

**Reference:** [docs/specs/01_Initial_Creation.md](../specs/01_Initial_Creation.md) (Phase 1 section)  
**Cursor prompts:** [docs/prompts/phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)  
**Storage format:** [docs/architecture/02-storage-format.md](../architecture/02-storage-format.md)

---

## Prerequisites

- Stage 0 complete (solution, projects, docs, Cursor rules)
- .NET 8 SDK
- Cross-platform: Windows, macOS, Linux

---

## Wave 1: Solution Foundation (Verify)

Stage 0 created the scaffolding. Verify and fix if needed.

- [ ] **1.1** Verify solution builds: `dotnet build`
- [ ] **1.2** Verify all test projects run: `dotnet test`
- [ ] **1.3** Verify project references match layered architecture (Engine → Contracts, Core, Storage, Parser; CLI → Engine)
- [ ] **1.4** Remove placeholder `PlaceholderTests.cs` only when real tests replace them (do not remove until Wave 8)

**Acceptance:** Solution builds with 0 errors; all placeholder tests pass.

---

## Wave 2: Contracts

Implement shared models and interfaces in SqlTxt.Contracts.

- [ ] **2.1** Implement `TableDefinition`, `ColumnDefinition`, `ColumnType` (CHAR with width)
- [ ] **2.2** Implement `RowData` or equivalent row abstraction
- [ ] **2.3** Implement `QueryResult`, `EngineResult` for execution output
- [ ] **2.4** Implement command objects: `CreateDatabaseCommand`, `CreateTableCommand`, `InsertCommand`, `SelectCommand`, `UpdateCommand`, `DeleteCommand`
- [ ] **2.5** Implement exception hierarchy: `SqlTxtException`, `ParseException`, `SchemaException`, `ValidationException`, `StorageException`, `ConstraintViolationException`
- [ ] **2.6** Define core interfaces: `IDatabaseEngine`, `ICommandParser`, `ISchemaStore`, `ITableDataStore`, `IMetadataStore`, `IFileSystemAccessor`, `IRowSerializer`, `IRowDeserializer` (stubs or full signatures)

**Acceptance:** Contracts project compiles; types are immutable where reasonable; interfaces are usable by dependent projects.

**Cursor prompt:** Wave 2 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 3: Storage Foundation

Implement filesystem-backed storage per the db/, Tables/, ~System/ layout.

- [ ] **3.1** Implement `IFileSystemAccessor` abstraction for file I/O (enables testability)
- [ ] **3.2** Create database root folder (database name) and `db/` folder with `manifest.json`
- [ ] **3.3** Create `Tables/` with one folder per table; each table folder contains `<TableName>.txt` (root data file)
- [ ] **3.4** Create `~System/` folder for system metadata (tables, columns)
- [ ] **3.5** Implement schema persistence: write/read schema (TABLE, FORMAT_VERSION, COLUMNS) — in table folder or ~System
- [ ] **3.6** Implement table metadata: ROW_COUNT, ACTIVE_ROW_COUNT, DELETED_ROW_COUNT, LAST_UPDATED_UTC
- [ ] **3.7** Implement row serialization: fixed-width positional format with soft-delete marker (A| or D|)
- [ ] **3.8** Ensure cross-platform paths (Windows, macOS, Linux)

**Acceptance:** Creating a database produces correct directory structure; creating a table produces table folder and schema; inserting a row appends correctly formatted data.

**Cursor prompt:** Wave 3 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 4: Parser v1

Implement tokenizer and parser for Phase 1 SQL subset.

- [ ] **4.1** Implement tokenizer: keywords, identifiers, literals (single-quoted strings), punctuation
- [ ] **4.2** Parse `CREATE DATABASE <name>;`
- [ ] **4.3** Parse `CREATE TABLE <name> (col1 type, col2 type, ...);` with CHAR(n) only
- [ ] **4.4** Parse `INSERT INTO <table> (cols) VALUES (vals);`
- [ ] **4.5** Parse `SELECT <cols|*> FROM <table> [WHERE col = 'literal'];`
- [ ] **4.6** Parse `UPDATE <table> SET col = 'literal' [, ...] [WHERE col = 'literal'];`
- [ ] **4.7** Parse `DELETE FROM <table> [WHERE col = 'literal'];`
- [ ] **4.8** Return strongly typed command objects from `ICommandParser`
- [ ] **4.9** Report parse errors with line/column when possible via `ParseException`

**Acceptance:** Parser returns correct command type for each statement; invalid syntax throws ParseException; WHERE supports equality-only.

**Cursor prompt:** Wave 4 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 5: Core Engine v1

Implement execution for CREATE DATABASE, CREATE TABLE, INSERT, SELECT.

- [ ] **5.1** Implement `IDatabaseEngine` (or equivalent) as main entry point
- [ ] **5.2** Execute `CREATE DATABASE`: create root folder, db/, manifest, Tables/, ~System/
- [ ] **5.3** Execute `CREATE TABLE`: create table folder, schema, metadata; register in ~System
- [ ] **5.4** Execute `INSERT`: validate schema, columns, widths; append row to `<TableName>.txt`; update metadata
- [ ] **5.5** Execute `SELECT`: read schema, scan data file, skip deleted rows, apply optional equality predicate, return projected columns
- [ ] **5.6** Validate column widths before insert; reject values longer than defined width
- [ ] **5.7** Use full table scan (no indexes in Phase 1)

**Acceptance:** End-to-end: create db, create table, insert rows, select all, select with WHERE — all persist and return correct data.

**Cursor prompt:** Wave 5 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 6: Update/Delete

Extend engine for UPDATE and DELETE.

- [ ] **6.1** Execute `UPDATE`: scan rows, match WHERE, rewrite/update row, enforce width validation
- [ ] **6.2** Execute `DELETE`: scan rows, match WHERE, mark as deleted (D|) — soft delete
- [ ] **6.3** Update metadata after insert, update, delete: ROW_COUNT, ACTIVE_ROW_COUNT, DELETED_ROW_COUNT, LAST_UPDATED_UTC
- [ ] **6.4** SELECT skips rows with D| marker

**Acceptance:** UPDATE modifies matching rows; DELETE marks rows deleted; SELECT excludes deleted rows; metadata counts are correct.

**Cursor prompt:** Wave 6 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 7: CLI / Consumer App

Implement CLI and update SampleApp.

- [ ] **7.1** CLI: `create-db <path>` — create database
- [ ] **7.2** CLI: `exec --db <path> "<statement>"` — execute single statement
- [ ] **7.3** CLI: `query --db <path> "<select>"` — execute SELECT and print result grid
- [ ] **7.4** CLI: `script --db <path> <file>` — execute script file
- [ ] **7.5** CLI: `inspect --db <path>` — print tables, columns, row counts (optional)
- [ ] **7.6** SampleApp: create database, create table, insert, select — demonstrate embedding

**Acceptance:** CLI can create db, run statements, run scripts; SampleApp demonstrates full flow.

**Cursor prompt:** Wave 7 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 8: Tests

Add unit and integration tests.

- [ ] **8.1** Parser unit tests: tokenization, each statement type, error cases
- [ ] **8.2** Storage unit tests: schema serialization, row format, metadata (mock IFileSystemAccessor)
- [ ] **8.3** Engine unit tests: validation, command execution (mocked storage)
- [ ] **8.4** Integration tests: create temp db, create table, insert, select, update, delete, verify file contents
- [ ] **8.5** Golden file tests (optional): known schema/data input → expected file output
- [ ] **8.6** Replace or remove PlaceholderTests with real tests

**Acceptance:** All tests pass; integration tests prove persisted text format matches spec.

**Cursor prompt:** Wave 8 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Wave 9: Hardening

Improve robustness and error handling.

- [ ] **9.1** Corruption handling: detect malformed schema, invalid metadata
- [ ] **9.2** Basic file locking on writes (single-process; prevent concurrent writes)
- [ ] **9.3** Clearer parser error messages (line, column, expected vs found)
- [ ] **9.4** Expand test coverage for edge cases (empty table, max width, invalid paths)
- [ ] **9.5** Update docs: Getting Started, CLI Reference, API docs per [05-documentation-standards.md](../architecture/05-documentation-standards.md)

**Acceptance:** Malformed input handled gracefully; parser errors are actionable; docs are current.

**Cursor prompt:** Wave 9 in [phase-1-cursor-prompts.md](../prompts/phase-1-cursor-prompts.md)

---

## Phase 1 Complete Checklist

- [ ] All waves 1–9 complete
- [ ] `dotnet build` succeeds
- [ ] `dotnet test` passes
- [ ] CLI can create db, run create-wiki.sql, run seed-wiki.sql
- [ ] SampleApp runs successfully
- [ ] README and docs updated

---

## Step 10: Phase 2 Plan Prompt

- [ ] **10.1** When Phase 1 is complete, use the following prompt to generate the Phase 2 implementation plan:

> **Prompt:** Using the plan at `docs/plans/Phase1_Implementation_Plan.md` and the specs in `docs/specs/01_Initial_Creation.md` (Phase 2 section), create a new Phase 2 implementation plan.
>
> The plan should:
> - Be saved to `docs/plans/Phase2_Implementation_Plan.md`
> - Follow the same format: each step as a referencable item with a checkbox
> - Cover: Primary keys, foreign keys, indexes, constraint enforcement, metadata expansion
> - Include concrete acceptance criteria per wave
> - Reference Phase 2 requirements from the spec
> - End with a step to prompt for the Phase 3 plan when Phase 2 is complete

---

## Clarification Questions for Phase 1 Completion

Before or during implementation, consider resolving:

1. **Semicolon:** Required or optional? Spec says "pick one and keep strict."
2. **CREATE DATABASE path:** Does `CREATE DATABASE DemoDb` create `./DemoDb` relative to current directory, or does the engine require an explicit base path? (CLI has `--db <path>`; API may need `Open(path)`.)
3. **Schema location:** Store schema in each table folder (e.g., `Tables/Users/schema.txt`) or in `~System`? Current storage doc allows either.
4. **Sample Wiki:** Should Phase 1 include running `create-wiki.sql` and `seed-wiki.sql` as a verification step, or is that a post-Phase-1 demo?
5. **INT type in PageContent.Version:** Sample Wiki uses `Version CHAR(10)` for Phase 1 compatibility. When INT is added (Phase 1.1?), update sample?

These can be decided during implementation or documented as decisions.
