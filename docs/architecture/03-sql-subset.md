# SQL Subset

## Phase 1 Supported Syntax

| Statement | Example |
|-----------|---------|
| CREATE DATABASE | `CREATE DATABASE DemoDb;` |
| CREATE TABLE | `CREATE TABLE Users (Id CHAR(10), Name CHAR(50));` |
| INSERT | `INSERT INTO Users (Id, Name) VALUES ('1', 'Richard');` |
| SELECT | `SELECT * FROM Users;` or `SELECT Id, Name FROM Users WHERE Id = '1';` |
| UPDATE | `UPDATE Users SET Name = 'Richard H' WHERE Id = '1';` |
| DELETE | `DELETE FROM Users WHERE Id = '1';` |

## Phase 1 Simplifications

- Case-insensitive keywords
- Identifiers case-preserving, normalized internally
- **Semicolon optional** — Parser accepts with or without
- String literals single-quoted
- No escaped quote support initially
- No comments required

## Phase 1 Data Types

- `CHAR(n)` — Fixed-width character
- `INT` — 32-bit integer
- `TINYINT` — 8-bit integer
- `BIGINT` — 64-bit integer
- `BIT` — Stored as `"1"` or `"0"`
- `DECIMAL(p,s)` — Fixed-width text; pad with zeros

## Phase 2 Additions

- `SELECT ... FROM table WITH (NOLOCK)` — Skip lock for read-only queries (faster)

## Phase 1 Exclusions

- Joins
- ORDER BY, GROUP BY
- Aggregates (COUNT, SUM, etc.)
- Functions, arithmetic expressions
- Transactions, concurrency
- Indexes, PK/FK
- VARCHAR, ALTER TABLE

## Reserved Words (Phase 1)

- CREATE, DATABASE, TABLE
- INSERT, INTO, VALUES
- SELECT, FROM, WHERE
- UPDATE, SET
- DELETE
- CHAR, INT, TINYINT, BIGINT, BIT, DECIMAL

## Parsing Assumptions

- Single-table only
- Equality-only predicates: `WHERE Column = 'literal'`
- Projection: `*` or explicit column list
- Fixed-width types only
