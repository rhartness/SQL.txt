# SQL.txt — Multi-Phase Specification

## 1. Project Overview

**SQL.txt** is a lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in human-readable text files.

The project has two goals:

1. **Learning tool**
   A readable, inspectable database engine for understanding storage engines, query parsing, CRUD semantics, metadata, schema evolution, and indexing.

2. **Practical embedded datastore**
   A minimal dependency, NuGet-installable local database that only requires:

   * a package reference
   * a writable directory
   * optional CLI / consumer tooling

---

## 2. Core Vision

### Primary Characteristics

* Human-readable on-disk storage
* Lightweight local embedded usage
* Easy to inspect and debug manually
* Structured progression from simple engine to more capable relational engine
* Implemented in phases with strict scope boundaries
* Query optimization sophistication (Baked in, but scale up capability with phases)

### Non-Goals for Initial Releases

* Enterprise-scale performance
* Full SQL standard support (EVENTUAL)
* Transactions with multi-version concurrency control
* Complex joins in Phase 1
* Advanced functions, aggregates, expressions, or subqueries in Phase 1
* Distributed storage in phase 2

---

## 3. High-Level Product Strategy

The project will be built in **stages**:

* **Stage 0:** Solution scaffolding, architecture, specifications, Cursor-ready prompts, test foundation
* **Phase 1:** Core engine with schema creation, metadata storage, simple parser, single-table CRUD, fixed-width fields only
* **Phase 2:** Indexes, PK/FK, constraints, relational metadata
* **Phase 3:** Variable-width fields (`VARCHAR`), storage evolution, extended parsing and validation

---

# Stage 0 — Solution Scaffolding and Design Foundation

## 1. Goals

Create the full Visual Studio solution structure, architecture, conventions, design documents, and prompt-ready implementation guidance for Cursor.  

## 2. Deliverables

* Visual Studio solution
* Core projects
* Test projects
* CLI / consumer app scaffolding
* Design specs
* Example database directory layout
* Coding conventions
* Test strategy
* Implementation backlog
* Cursor-oriented build prompts

## 3. Proposed Solution Structure

```text
SqlTxt.sln

src/
  SqlTxt.Core/
  SqlTxt.Storage/
  SqlTxt.Parser/
  SqlTxt.Engine/
  SqlTxt.Contracts/
  SqlTxt.Cli/
  SqlTxt.SampleApp/

tests/
  SqlTxt.Core.Tests/
  SqlTxt.Storage.Tests/
  SqlTxt.Parser.Tests/
  SqlTxt.Engine.Tests/
  SqlTxt.IntegrationTests/

docs/
  architecture/
  specifications/
  prompts/
  decisions/
  examples/
```

## 4. Project Responsibilities

### `SqlTxt.Contracts`

Shared models and interfaces:

* table definitions
* column definitions
* row abstractions
* query command contracts
* engine result contracts
* exceptions

### `SqlTxt.Core`

Low-level domain logic and primitives:

* data types
* validation rules
* identifier normalization
* serialization helpers
* common utilities

### `SqlTxt.Storage`

On-disk persistence:

* file naming
* directory structure
* schema read/write
* metadata read/write
* row file read/write
* locking strategy abstraction
* format version management

### `SqlTxt.Parser`

Text command parsing:

* tokenizer
* parser
* command AST / DTO output
* error reporting with line/column when possible

### `SqlTxt.Engine`

Execution layer:

* create database
* create table
* insert
* select
* update
* delete
* metadata coordination
* table scans
* index usage later

### `SqlTxt.Cli`

User-facing command line tool:

* create db
* execute SQL.txt commands
* inspect metadata
* print results

### `SqlTxt.SampleApp`

Minimal consumer example:

* initialize database
* create tables
* insert and query rows
* demonstrate NuGet-style embedding target

### Test Projects

Unit and integration test coverage split by concern.

---

## 5. Initial Architectural Style

Use a **layered architecture**:

* Parser layer
* Engine layer
* Storage layer
* File system layer

Keep the engine decoupled from file I/O through interfaces.

### Recommended Core Interfaces

```csharp
IDatabaseEngine
ICommandParser
ISchemaStore
ITableDataStore
IMetadataStore
IFileSystemAccessor
IRowSerializer
IRowDeserializer
```

---

## 6. Cursor Design Docs to Include

## `docs/specifications/00-product-spec.md`

* purpose
* scope
* phased roadmap
* user stories
* constraints

## `docs/architecture/01-system-architecture.md`

* projects
* dependencies
* layering
* storage design
* parser design
* extensibility

## `docs/architecture/02-storage-format.md`

* directory layout
* file naming
* schema file format
* metadata file format
* row data format
* versioning strategy

## `docs/architecture/03-sql-subset.md`

* supported syntax by phase
* unsupported syntax
* reserved words
* parsing assumptions

## `docs/architecture/04-testing-strategy.md`

* unit tests
* integration tests
* golden file tests
* parser cases
* corruption handling

## `docs/prompts/phase-1-cursor-prompts.md`

Cursor-ready prompts for each wave.

## `docs/decisions/adr-001-human-readable-storage.md`

Architecture decision records for major choices.

---

# Phase 1 — Minimal Readable Database Engine

## 1. Phase Goal

Deliver a functioning embedded text-file database engine that supports:

* database creation
* table creation
* schema persistence
* metadata persistence
* single-table insert/select/update/delete
* fixed-width field storage
* simple SQL-like parser
* no joins
* no indexes
* no aggregates
* no expressions beyond simple equality filtering

---

## 2. Scope

## Included

* `CREATE DATABASE`
* `CREATE TABLE`
* `INSERT`
* `SELECT`
* `UPDATE`
* `DELETE`
* `WHERE Column = Literal`
* fixed-width column types only
* full table scan execution
* metadata tables persisted to disk
* table files stored as readable text

## Excluded

* joins
* ordering
* grouping
* aggregates
* functions
* arithmetic expressions
* transactions
* concurrency beyond basic file safety
* indexes
* PK/FK enforcement
* `VARCHAR`
* `ALTER TABLE`

---

## 3. Phase 1 Functional Requirements

## 3.1 Database Lifecycle

### Create Database

A database is created by initializing a root directory and writing metadata files.

**Example**

```sql
CREATE DATABASE DemoDb;
```

### Expected Result

Creates:

* root folder
* database manifest
* schema folder
* table folder
* metadata folder

---

## 3.2 Table Creation

### Supported Example

```sql
CREATE TABLE Users (
    Id CHAR(10),
    Name CHAR(50),
    Email CHAR(100)
);
```

### Rules

* table names are unique within a database
* column names are unique within a table
* only fixed-width types in Phase 1
* widths required and validated
* column order preserved exactly as defined

---

## 3.3 Insert

### Example

```sql
INSERT INTO Users (Id, Name, Email)
VALUES ('1', 'Richard', 'richard@example.com');
```

### Behavior

* values are mapped to columns by provided order
* missing columns may be padded as empty if allowed by initial design
* values longer than column width cause validation failure
* stored values are padded to fixed width

---

## 3.4 Select

### Examples

```sql
SELECT * FROM Users;
SELECT Id, Name FROM Users;
SELECT * FROM Users WHERE Id = '1';
```

### Behavior

* single-table only
* no joins
* no sorting
* no aggregates
* projection allowed
* equality-only predicate for initial wave

---

## 3.5 Update

### Example

```sql
UPDATE Users
SET Name = 'Richard H'
WHERE Id = '1';
```

### Behavior

* single-table scan
* equality filter only
* one or more column assignments allowed
* width validation enforced before write

---

## 3.6 Delete

### Example

```sql
DELETE FROM Users WHERE Id = '1';
```

### Behavior

* single-table scan
* equality filter only
* matching rows removed or marked deleted depending on storage strategy

---

## 4. Phase 1 SQL Subset

## Statements

* `CREATE DATABASE <name>;`
* `CREATE TABLE <name> (...);`
* `INSERT INTO <table> (...) VALUES (...);`
* `SELECT <columns|*> FROM <table> [WHERE <column> = <literal>];`
* `UPDATE <table> SET <column> = <literal>[, ...] [WHERE <column> = <literal>];`
* `DELETE FROM <table> [WHERE <column> = <literal>];`

## Initial Simplifications

* case-insensitive keywords
* identifiers may be case-preserving but normalized internally
* semicolon optional or required; pick one and keep strict
* string literals single-quoted
* no escaped quote support initially unless easy to include
* no comments required in first parser wave

---

## 5. Phase 1 Data Types

## Required Initial Types

Use a deliberately narrow set first:

* `CHAR(n)`
* optional numeric fixed-width types later in Phase 1.1 if desired

### Recommendation

Start with **all values internally treated as strings** for the first working version.

Then add typed validation in a later wave:

* `INT`
* `BOOL`
* `DATE`

That keeps the first implementation smaller and lets parsing/storage stabilize early.

---

## 6. On-Disk Storage Design

## 6.1 Directory Layout

```text
/MyDatabase/
  db.config.json
  /schemas/
    Users.schema.txt
  /tables/
    Users.data.txt
  /meta/
    tables.meta.txt
    columns.meta.txt
```

Alternative:

```text
/MyDatabase/
  manifest.txt
  /tables/
    Users/
      schema.txt
      data.txt
```

### Recommendation

Use **per-table folders** because Phase 2 and 3 will need:

* index files
* relationship metadata
* stats
* format upgrades

Preferred structure:

```text
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

---

## 6.2 Schema File Format

Human-readable, deterministic, easy to diff.

### Example `schema.txt`

```text
TABLE: Users
FORMAT_VERSION: 1
COLUMNS:
1|Id|CHAR|10
2|Name|CHAR|50
3|Email|CHAR|100
```

---

## 6.3 Table Metadata File

### Example `table.meta.txt`

```text
TABLE: Users
ROW_COUNT: 12
ACTIVE_ROW_COUNT: 12
DELETED_ROW_COUNT: 0
LAST_UPDATED_UTC: 2026-03-16T00:00:00Z
```

---

## 6.4 Data File Format

You want readability, but also structured parsing. Two good options:

### Option A — Delimited readable records

```text
1|Richard                                           |richard@example.com
2|Alice                                             |alice@example.com
```

### Option B — Named field records

```text
Id=1|Name=Richard|Email=richard@example.com
```

### Recommendation

For fixed-width Phase 1, use **positional padded format** with schema-driven widths.

That gives:

* compact storage
* deterministic parsing
* strong link to fixed-width concept
* good learning value

Example row in raw file:

```text
1         Richard                                           richard@example.com
```

To preserve readability, also include a header file or schema file that explains widths.

---

## 7. Record Deletion Strategy

Two options:

### Hard Delete

Physically remove rows from file.

**Pros**

* simpler visible semantics
* smaller files

**Cons**

* rewrite cost on delete
* harder future recovery

### Soft Delete Flag

Prefix each row with status marker:

```text
A|1         Richard...
D|2         Alice...
```

**Recommendation**
Use **soft delete marker** in early implementation. It makes update/delete logic simpler and prepares for future indexing and compaction.

---

## 8. Metadata Strategy

Maintain system metadata files for:

* tables
* columns
* storage format version

This provides a database-like introspection experience and helps future CLI tooling.

### Example `tables.meta.txt`

```text
Users|2026-03-16T00:00:00Z
Orders|2026-03-16T00:05:00Z
```

### Example `columns.meta.txt`

```text
Users|1|Id|CHAR|10
Users|2|Name|CHAR|50
Users|3|Email|CHAR|100
```

---

## 9. Execution Model

## Insert

* validate schema exists
* validate columns
* validate widths
* normalize values
* append row to data file
* update metadata

## Select

* read schema
* scan data file
* skip deleted rows
* apply optional equality predicate
* return projected columns

## Update

* scan all rows
* identify matching rows
* rewrite file or append replacement rows depending on storage strategy
* update metadata

## Delete

* scan rows
* mark as deleted
* update counts

---

## 10. Recommended Internal Domain Models

```csharp
DatabaseDefinition
TableDefinition
ColumnDefinition
ColumnType
RowData
QueryCommand
CreateDatabaseCommand
CreateTableCommand
InsertCommand
SelectCommand
UpdateCommand
DeleteCommand
QueryResult
EngineResult
```

---

## 11. Testing Strategy for Phase 1

## Unit Tests

* parser tokenization
* parser statement parsing
* identifier validation
* width validation
* row formatting/parsing
* metadata serialization

## Integration Tests

* create database writes expected files
* create table writes schema and metadata
* insert appends correctly
* select returns expected rows
* update modifies matching rows
* delete marks/removes rows
* re-open existing db and query persisted data

## Golden File Tests

Use known input and expected output files:

* schema files
* data files
* metadata files

These are especially useful for Cursor-driven implementation.

---

## 12. Phase 1 Suggested Implementation Waves

## Wave 1 — Solution Foundation

* scaffold solution
* create interfaces
* create contracts
* create empty CLI
* create test projects
* add docs

## Wave 2 — Storage Foundation

* database directory creation
* manifest writer
* schema writer/reader
* metadata writer/reader

## Wave 3 — Parser v1

* tokenizer
* parse create db
* parse create table
* parse insert/select/update/delete

## Wave 4 — Core Engine v1

* execute create db
* execute create table
* execute insert
* execute select

## Wave 5 — Update/Delete

* update row rewrite strategy
* delete marker strategy
* metadata counts

## Wave 6 — CLI / Consumer App

* run commands from console
* execute script files
* print result grids
* sample app integration

## Wave 7 — Hardening

* corruption handling
* malformed schema detection
* file locking
* clearer parser errors
* test coverage expansion

---

# Phase 2 — Indexes, Keys, and Relational Metadata

## 1. Phase Goal

Add relational structure and faster lookup while keeping files readable.

## 2. Scope

* primary keys
* foreign keys
* unique constraints if desired
* index files
* metadata enhancements
* basic constraint enforcement

## 3. Functional Requirements

## 3.1 Primary Keys

Allow:

```sql
CREATE TABLE Users (
    Id CHAR(10) PRIMARY KEY,
    Name CHAR(50)
);
```

or table-level:

```sql
PRIMARY KEY (Id)
```

### Behavior

* enforce uniqueness
* prevent null/empty if null semantics are added
* create PK index file

---

## 3.2 Foreign Keys

Allow:

```sql
CREATE TABLE Orders (
    OrderId CHAR(10),
    UserId CHAR(10),
    FOREIGN KEY (UserId) REFERENCES Users(Id)
);
```

### Behavior

* validate referenced table/column exist
* validate insert/update referential integrity
* prevent parent delete if dependent rows exist unless later cascade rules are added

---

## 3.3 Indexes

Support:

```sql
CREATE INDEX IX_Users_Name ON Users(Name);
```

### Index Goals

* speed equality lookups
* readable on disk
* rebuildable

### Possible Index File

```text
VALUE|ROW_POINTERS
Richard|5,20,44
Alice|6,9
```

Or one entry per line:

```text
Richard|5
Richard|20
Richard|44
Alice|6
Alice|9
```

### Recommendation

Start with one-entry-per-line for simplicity and readability.

---

## 3.4 Metadata Expansion

Add:

* key definitions
* index definitions
* relationship definitions

---

## 4. Technical Considerations

* define row pointer strategy
* stable row identifiers may be needed
* soft delete compaction becomes more important
* update paths must refresh indexes
* FK checks require dependent lookups

---

## 5. Phase 2 Risks

* row position changes can invalidate indexes
* file rewrites may be expensive
* FK enforcement complexity rises quickly

### Recommendation

Introduce an internal immutable row id in Phase 2:

* `_RowId`
* not user-visible initially
* used for index targeting

---

# Phase 3 — Variable-Length Fields (`VARCHAR`)

## 1. Phase Goal

Expand storage model to support variable-width fields without breaking readability.

## 2. Scope

* `VARCHAR(n)`
* mixed fixed and variable-width row handling
* parser/storage updates
* validation and serialization strategy

## 3. Functional Requirements

### Example

```sql
CREATE TABLE Notes (
    Id CHAR(10),
    Title VARCHAR(100),
    Body VARCHAR(1000)
);
```

---

## 4. Storage Design Options

### Option A — Delimited Row Storage

```text
1|Hello|This is body text
```

**Pros**

* easy to read
* easy to append

**Cons**

* escaping complexity
* delimiter collision issues

### Option B — Length-Prefixed Segments

```text
Id=1;Title[5]=Hello;Body[17]=This is body text
```

**Pros**

* deterministic parsing
* avoids delimiter ambiguity better

**Cons**

* noisier format

### Option C — Hybrid Per-Row JSON-ish Text

Human-readable but likely too big and less aligned with earlier phases.

### Recommendation

For Phase 3, move to a **schema-aware delimited format with escaping**, or a **length-prefixed readable format**.

If preserving engine simplicity matters most, choose:
**length-prefixed readable records**.

---

## 5. Compatibility

Need a storage format version field so Phase 1/2 fixed-width tables continue to work.

### Recommendation

Support per-table format versions:

* fixed-width tables remain unchanged
* varchar-enabled tables use upgraded serializer

---

# Cross-Phase Technical Standards

## 1. Language / Platform

Recommended:

* .NET 8 for engine and CLI
* C# class libraries
* keep APIs usable from older consumer apps if needed later via compatible package strategy

## 2. Error Handling

Define clear exception hierarchy:

* `SqlTxtException`
* `ParseException`
* `SchemaException`
* `ValidationException`
* `StorageException`
* `ConstraintViolationException`

## 3. Logging

Optional in Phase 1.
Keep via abstraction:

```csharp
ISqlTxtLogger
```

## 4. File Access / Concurrency

For early phases:

* single-process friendly
* basic file lock on write operations
* no strong multi-process guarantees initially

## 5. Versioning

Add engine/storage version file early:

```json
{
  "engineVersion": "0.1.0",
  "storageFormatVersion": 1
}
```

---

# Consumer Experience Specification

## 1. NuGet Consumer Goal

A user should be able to do something like:

```csharp
var engine = SqlTxtDatabase.Open("C:\\Data\\MyDb");
engine.Execute("CREATE TABLE Users (Id CHAR(10), Name CHAR(50));");
engine.Execute("INSERT INTO Users (Id, Name) VALUES ('1', 'Richard');");
var result = engine.ExecuteQuery("SELECT * FROM Users;");
```

## 2. CLI Goal

```bash
sqltxt create-db DemoDb
sqltxt exec "CREATE TABLE Users (Id CHAR(10), Name CHAR(50));"
sqltxt exec "INSERT INTO Users (Id, Name) VALUES ('1', 'Richard');"
sqltxt query "SELECT * FROM Users;"
```

---

# Recommended Initial Backlog

## Epic 1 — Foundation

* create solution
* establish projects
* create contracts
* create docs
* setup CI
* setup test base

## Epic 2 — Storage

* manifest format
* schema format
* metadata format
* data format
* storage abstractions

## Epic 3 — Parser

* tokenizer
* AST contracts
* parser for supported statements
* syntax errors

## Epic 4 — Engine

* command execution
* validation
* scans
* row persistence

## Epic 5 — Tooling

* CLI
* sample app
* examples
* script execution

## Epic 6 — Constraints and Indexes

* PK
* FK
* index maintenance

## Epic 7 — Flexible Data Types

* varchar
* serializer evolution
* migration/version handling

---

# Cursor-Oriented Implementation Guidance

## Cursor Prompt Strategy

Break work into very small, testable chunks. Do not ask Cursor for “build the whole database engine.” Give it bounded tasks.

## Example Prompt Set

### Prompt 1 — Solution Scaffold

> Create a Visual Studio solution named SqlTxt with the following projects: SqlTxt.Contracts, SqlTxt.Core, SqlTxt.Storage, SqlTxt.Parser, SqlTxt.Engine, SqlTxt.Cli, SqlTxt.SampleApp, SqlTxt.Core.Tests, SqlTxt.Storage.Tests, SqlTxt.Parser.Tests, SqlTxt.Engine.Tests, SqlTxt.IntegrationTests. Use .NET 8. Add project references according to a layered architecture where Engine depends on Contracts/Core/Storage/Parser, and CLI depends on Engine. Add placeholder README files and test classes.

### Prompt 2 — Contracts

> Implement the initial contract models for TableDefinition, ColumnDefinition, ColumnType, QueryResult, EngineResult, and command objects for CreateDatabase, CreateTable, Insert, Select, Update, and Delete. Keep them immutable where reasonable.

### Prompt 3 — Storage

> Implement a filesystem-backed storage layer for SqlTxt that creates a database root folder, writes a manifest file, writes table schema.txt files, and writes metadata files for tables and columns. Keep all formats human-readable text.

### Prompt 4 — Parser

> Implement a tokenizer and parser for a constrained SQL subset supporting CREATE DATABASE, CREATE TABLE, INSERT, SELECT, UPDATE, and DELETE. Support simple WHERE Column = 'literal' only. Return strongly typed command objects.

### Prompt 5 — Engine

> Implement execution for CREATE DATABASE, CREATE TABLE, INSERT, and SELECT over single tables using full table scans and fixed-width CHAR(n) storage.

### Prompt 6 — Update/Delete

> Extend the engine to support UPDATE and DELETE with equality-only WHERE clauses. Use a row status marker for soft deletion and update metadata counts.

### Prompt 7 — Tests

> Add integration tests that create a temp database directory, create tables, insert rows, select rows, update rows, delete rows, and verify the exact contents of the generated text files.

---

# Open Design Decisions

These are the main questions worth resolving before implementation gets far:

## 1. Strict SQL vs SQL-like?

Should syntax be:

* intentionally SQL-like but limited, or
* strict subset of SQL

**Recommendation:** strict SQL-like subset with clearly documented deviations.

## 2. Case Sensitivity

Should identifiers be case-sensitive on disk and in execution?

**Recommendation:** case-insensitive lookup, case-preserving display.

## 3. Null Semantics

Do you want `NULL` in Phase 1?

**Recommendation:** no real null support initially; treat omitted/blank as empty string until types mature.

## 4. Delete Strategy

Soft delete or hard delete?

**Recommendation:** soft delete first, compaction later.

## 5. Script Support

Single statements only, or multi-statement script files in Phase 1?

**Recommendation:** allow multi-statement script execution in CLI, even if engine executes one statement at a time.

## 6. Storage Format

Pure fixed-width rows or field-labeled rows?

**Recommendation:** pure fixed-width for Phase 1, field-labeled or length-prefixed hybrid later.

---

# Recommended First Milestone

## Milestone 0.1

A working demo with:

* create db
* create table with `CHAR(n)`
* insert rows
* select all rows
* select rows with equality filter
* sample CLI
* integration tests proving persisted text format

That is the first meaningful checkpoint. Do not add PK/FK/indexes before this is solid.

---

# Suggested Repository Structure

```text
/README.md
/LICENSE
/.gitignore
/global.json
/Directory.Build.props

/docs/
  /architecture/
  /specifications/
  /prompts/
  /decisions/
  /examples/

/src/
  /SqlTxt.Contracts/
  /SqlTxt.Core/
  /SqlTxt.Storage/
  /SqlTxt.Parser/
  /SqlTxt.Engine/
  /SqlTxt.Cli/
  /SqlTxt.SampleApp/

/tests/
  /SqlTxt.Core.Tests/
  /SqlTxt.Storage.Tests/
  /SqlTxt.Parser.Tests/
  /SqlTxt.Engine.Tests/
  /SqlTxt.IntegrationTests/
```

---

# Final Recommendation

Build this in this exact order:

1. **Docs + solution scaffold**
2. **Filesystem storage contracts**
3. **Schema persistence**
4. **Parser for a tiny SQL subset**
5. **Insert/select**
6. **Update/delete**
7. **CLI and sample app**
8. **Hardening**
9. **Indexes / PK / FK**
10. **VARCHAR**

That order keeps the project teachable, testable, and realistic.

I can turn this next into a **Cursor-ready implementation pack** with:

* phased backlog
* project-by-project responsibilities
* concrete user stories
* acceptance criteria
* first-wave prompts
* initial class/interface list
