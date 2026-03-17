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
- Semicolon required (or optional; pick one and stay consistent)
- String literals single-quoted
- No escaped quote support initially
- No comments required

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
- CHAR

## Parsing Assumptions

- Single-table only
- Equality-only predicates: `WHERE Column = 'literal'`
- Projection: `*` or explicit column list
- Fixed-width types only: `CHAR(n)` with required width
