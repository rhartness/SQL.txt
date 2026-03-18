# SQL Subset

SQL.txt aligns with **SQL:2023** (ISO/IEC 9075-2:2023). See [11-sql2023-mapping.md](11-sql2023-mapping.md) for per-phase feature mapping.

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

- **CREATE DATABASE:** `CREATE DATABASE name WITH (defaultMaxShardSize=20971520);` — Database-level shard size default (20 MB)
- **CREATE TABLE:** `PRIMARY KEY` (column-level or table-level), `FOREIGN KEY (col) REFERENCES Table(col)`, `UNIQUE` (column-level or table-level); `WITH (maxShardSize=...)` for per-table override
- **CREATE INDEX:** `CREATE INDEX IX_Name ON Table(Column);` and `CREATE UNIQUE INDEX ...`
- **SELECT:** `SELECT ... FROM table WITH (NOLOCK)` — Skip lock for read-only queries (faster)

## CTE Phase Additions

- **WITH clause:** `WITH cte AS (SELECT ...) SELECT * FROM cte;` — Non-recursive Common Table Expressions
- **Recursive CTE:** `WITH RECURSIVE cte AS (anchor UNION ALL recursive) SELECT * FROM cte;`

## Phase 1 Exclusions (Phase 2 adds indexes, PK/FK)

- Joins
- ORDER BY, GROUP BY
- Aggregates (COUNT, SUM, etc.)
- Functions, arithmetic expressions
- Transactions
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
