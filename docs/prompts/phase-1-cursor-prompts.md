# Phase 1 Cursor Prompts

Use these prompts for bounded, testable implementation waves. Do not combine multiple waves in a single prompt.

---

## Wave 1 — Solution Foundation

> Create a Visual Studio solution named SqlTxt with the following projects: SqlTxt.Contracts, SqlTxt.Core, SqlTxt.Storage, SqlTxt.Parser, SqlTxt.Engine, SqlTxt.Cli, SqlTxt.SampleApp, SqlTxt.Core.Tests, SqlTxt.Storage.Tests, SqlTxt.Parser.Tests, SqlTxt.Engine.Tests, SqlTxt.IntegrationTests. Use .NET 8. Add project references according to a layered architecture where Engine depends on Contracts/Core/Storage/Parser, and CLI depends on Engine. Add placeholder README files and test classes.

*Note: Stage 0 has already created the solution. Use this prompt only if scaffolding needs to be recreated.*

---

## Wave 2 — Contracts

> Implement the initial contract models for SqlTxt.Contracts: TableDefinition, ColumnDefinition, ColumnType (CHAR, INT, TINYINT, BIGINT, BIT, DECIMAL), QueryResult, EngineResult, and command objects for CreateDatabase (with optional NumberFormat, TextEncoding), CreateTable, Insert, Select, Update, and Delete. Add MaxShardSize to table definition. Define IDatabaseEngine with async methods (ExecuteAsync, ExecuteQueryAsync, OpenAsync). Define IDatabaseLockManager for Phase 1 locking. Keep them immutable where reasonable. Add the exception hierarchy: SqlTxtException, ParseException, SchemaException, ValidationException, StorageException, ConstraintViolationException — all support file name, row number, character position when applicable.

---

## Wave 3 — Storage Foundation

> Implement a filesystem-backed storage layer for SqlTxt per docs/architecture/02-storage-format.md. Create database root folder (database name), db/ folder with manifest.json (include numberFormat, textEncoding). Tables/ with one folder per table; each contains <TableName>.txt (root data). Schema in BOTH ~System (master) and table folder (reference copy); engine reads from ~System. Create ~System/ for system metadata. Row format: BIT as "1"/"0", DECIMAL padded with zeros. Add sharding: when table exceeds MaxShardSize, create new shard. Use IFileSystemAccessor for testability. Cross-platform (Windows, macOS, Linux).

---

## Wave 4 — Parser v1

> Implement a tokenizer and parser for a constrained SQL subset supporting CREATE DATABASE (with optional WITH numberFormat, textEncoding), CREATE TABLE (CHAR, INT, TINYINT, BIGINT, BIT, DECIMAL), INSERT, SELECT, UPDATE, and DELETE. Semicolon optional. Support simple WHERE Column = 'literal' only. Return strongly typed command objects from ICommandParser. Report parse errors with line/column when possible.

---

## Wave 5 — Core Engine v1

> Implement execution for CREATE DATABASE, CREATE TABLE, INSERT, and SELECT over single tables using full table scans and fixed-width storage. Use async API (ExecuteAsync, ExecuteQueryAsync, OpenAsync). Implement IDatabaseLockManager with single mutex per database; acquire before write, release after. Use the storage layer for persistence. Validate schema, columns, and widths before writes.

---

## Wave 6 — Update/Delete

> Extend the engine to support UPDATE and DELETE with equality-only WHERE clauses. Use a row status marker (A| or D|) for soft deletion. Update metadata counts (ROW_COUNT, ACTIVE_ROW_COUNT, DELETED_ROW_COUNT) after insert, update, and delete.

---

## Wave 6.5 — NuGet Packaging

> Add NuGet package project (SqlTxt) that packages Engine + Contracts, Core, Storage, Parser. Configure package metadata (id: SqlTxt). Ensure CLI and SampleApp consume Engine via project reference.

## Wave 7 — CLI / Consumer App

> Implement the CLI to run commands from console, execute script files, and print result grids. Update SqlTxt.SampleApp to initialize a database, create tables, insert and query rows, demonstrating NuGet-style embedding. Use async engine API.

---

## Wave 8 — Tests

> Test-first paradigm. Full unit test coverage per functional implementation. Add unit tests for parser (tokenization, each statement type, high/low values, exception paths), storage (schema, row format, all data types, mock IFileSystemAccessor), engine (validation, mocked storage). Add integration tests: create temp db, create table, insert, select, update, delete, verify file contents. Test corrupted file handling: verify exceptions include file name, row number, character position.

---

## Wave 9 — Hardening

> Add corruption handling: when errors occur, provide file name, row number, character position. Exception messages must explain how user interaction (e.g., manual file edit) might have caused the issue. Basic file locking on writes. Clearer parser error messages. Expand test coverage for edge cases. Update docs per 05-documentation-standards.md.
