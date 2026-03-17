# Phase 1 Cursor Prompts

Use these prompts for bounded, testable implementation waves. Do not combine multiple waves in a single prompt.

---

## Wave 1 — Solution Foundation

> Create a Visual Studio solution named SqlTxt with the following projects: SqlTxt.Contracts, SqlTxt.Core, SqlTxt.Storage, SqlTxt.Parser, SqlTxt.Engine, SqlTxt.Cli, SqlTxt.SampleApp, SqlTxt.Core.Tests, SqlTxt.Storage.Tests, SqlTxt.Parser.Tests, SqlTxt.Engine.Tests, SqlTxt.IntegrationTests. Use .NET 8. Add project references according to a layered architecture where Engine depends on Contracts/Core/Storage/Parser, and CLI depends on Engine. Add placeholder README files and test classes.

*Note: Stage 0 has already created the solution. Use this prompt only if scaffolding needs to be recreated.*

---

## Wave 2 — Contracts

> Implement the initial contract models for SqlTxt.Contracts: TableDefinition, ColumnDefinition, ColumnType, QueryResult, EngineResult, and command objects for CreateDatabase, CreateTable, Insert, Select, Update, and Delete. Keep them immutable where reasonable. Add the exception hierarchy: SqlTxtException, ParseException, SchemaException, ValidationException, StorageException, ConstraintViolationException.

---

## Wave 3 — Storage Foundation

> Implement a filesystem-backed storage layer for SqlTxt that creates a database root folder, writes a manifest file (db.manifest.json), writes table schema.txt files, and writes metadata files for tables and columns. Keep all formats human-readable text per docs/architecture/02-storage-format.md. Use IFileSystemAccessor to abstract file I/O for testability.

---

## Wave 4 — Parser v1

> Implement a tokenizer and parser for a constrained SQL subset supporting CREATE DATABASE, CREATE TABLE, INSERT, SELECT, UPDATE, and DELETE. Support simple WHERE Column = 'literal' only. Return strongly typed command objects from ICommandParser. Report parse errors with line/column when possible.

---

## Wave 5 — Core Engine v1

> Implement execution for CREATE DATABASE, CREATE TABLE, INSERT, and SELECT over single tables using full table scans and fixed-width CHAR(n) storage. Use the storage layer for persistence. Validate schema, columns, and widths before writes.

---

## Wave 6 — Update/Delete

> Extend the engine to support UPDATE and DELETE with equality-only WHERE clauses. Use a row status marker (A| or D|) for soft deletion. Update metadata counts (ROW_COUNT, ACTIVE_ROW_COUNT, DELETED_ROW_COUNT) after insert, update, and delete.

---

## Wave 7 — CLI / Consumer App

> Implement the CLI to run commands from console, execute script files, and print result grids. Update SqlTxt.SampleApp to initialize a database, create tables, insert and query rows, demonstrating NuGet-style embedding.

---

## Wave 8 — Tests

> Add integration tests that create a temp database directory, create tables, insert rows, select rows, update rows, delete rows, and verify the exact contents of the generated text files. Add unit tests for parser tokenization, storage serialization, and engine validation.

---

## Wave 9 — Hardening

> Add corruption handling, malformed schema detection, basic file locking on writes, and clearer parser error messages. Expand test coverage for edge cases.
