# SQL.txt — Post-Phase 3 Features Specification

## Purpose

This document extends the SQL.txt roadmap beyond Phase 3 (VARCHAR, variable-width fields, storage evolution). The goal is to move SQL.txt toward a workable RDBMS for simple projects—learning, prototyping, and small applications—while preserving human-readable storage and incremental design.

## Feature List

| Feature | Phase | Description |
|---------|-------|-------------|
| **JOINs** | 4 | INNER, LEFT (OUTER) joins across tables |
| **Aggregates** | 4 | COUNT, SUM, AVG, MIN, MAX |
| **ORDER BY** | 4 | Sort results by column(s); ASC/DESC |
| **GROUP BY** | 4 | Group rows; optional HAVING |
| **Subqueries** | 4 | IN (list), EXISTS, scalar in SELECT/WHERE |
| **ALTER TABLE** | 5 | ADD COLUMN, DROP COLUMN, RENAME COLUMN/TABLE |
| **Transactions** | 5 | BEGIN TRANSACTION, COMMIT, ROLLBACK |
| **Views** | 6 | CREATE VIEW; stored SELECT definitions |
| **Stored Procedures** | 6 | CREATE PROCEDURE, EXEC; multi-statement |
| **Functions** | 6 | CREATE FUNCTION; scalar user-defined functions |

---

## Phase 4 — Query Enrichment

Foundation for Views and complex procedures. Extends the SELECT engine beyond single-table scans.

### 4.1 JOINs

**Syntax**

- `INNER JOIN` — Rows matching on join condition
- `LEFT [OUTER] JOIN` — All rows from left table; matching rows from right (or NULL)

**Example**

```sql
SELECT u.Id, u.Name, o.OrderId
FROM Users u
INNER JOIN Orders o ON u.Id = o.UserId;

SELECT u.Id, u.Name, o.OrderId
FROM Users u
LEFT JOIN Orders o ON u.Id = o.UserId;
```

**Execution model**

- Nested-loop join or hash join (implementation choice)
- Join condition: equality on column(s)

**Scope**

- Two-table joins initially; multi-table joins (A JOIN B JOIN C) as extension
- JOIN ON only; no USING in initial scope

### 4.2 Aggregates

**Functions**

- `COUNT(*)` — Row count
- `COUNT(column)` — Non-NULL count
- `SUM(column)` — Sum of numeric column
- `AVG(column)` — Average of numeric column
- `MIN(column)` — Minimum value
- `MAX(column)` — Maximum value

**Example**

```sql
SELECT COUNT(*) FROM Users;
SELECT UserId, COUNT(*) FROM Orders GROUP BY UserId;
```

### 4.3 ORDER BY

**Syntax**

- `ORDER BY column [ASC|DESC] [, column [ASC|DESC] ...]`
- Default ASC

**Example**

```sql
SELECT Id, Name FROM Users ORDER BY Name ASC;
SELECT Id, Name FROM Users ORDER BY Name DESC, Id ASC;
```

### 4.4 GROUP BY

**Syntax**

- `GROUP BY column [, column ...]`
- Aggregates apply to each group

**Optional HAVING**

- `HAVING condition` — Filter groups after aggregation

**Example**

```sql
SELECT UserId, COUNT(*) AS OrderCount
FROM Orders
GROUP BY UserId
HAVING COUNT(*) > 5;
```

### 4.5 Subqueries

**Forms**

1. **IN (list)** — `WHERE column IN (SELECT ...)`
2. **EXISTS** — `WHERE EXISTS (SELECT 1 FROM ... WHERE ...)`
3. **Scalar** — Single-value subquery in SELECT or WHERE

**Example**

```sql
SELECT * FROM Users WHERE Id IN (SELECT UserId FROM Orders);
SELECT * FROM Users u WHERE EXISTS (SELECT 1 FROM Orders o WHERE o.UserId = u.Id);
SELECT Id, (SELECT COUNT(*) FROM Orders WHERE UserId = Users.Id) AS OrderCount FROM Users;
```

**Constraints**

- Scalar subqueries must return exactly one row (or NULL)
- Correlated subqueries supported

---

## Phase 5 — Schema Evolution and Transactions

### 5.1 ALTER TABLE

**Operations**

- `ADD COLUMN column type [constraints]` — Add column after existing columns
- `DROP COLUMN column` — Remove column from schema
- `RENAME COLUMN old TO new` — Rename column
- `RENAME TO new_table` — Rename table

**Example**

```sql
ALTER TABLE Users ADD COLUMN Phone CHAR(20);
ALTER TABLE Users DROP COLUMN Phone;
ALTER TABLE Users RENAME COLUMN Email TO EmailAddress;
ALTER TABLE Users RENAME TO AppUsers;
```

**Storage impact**

- Schema files updated; data files may require migration for ADD/DROP
- Indexes updated for renamed columns

### 5.2 Transactions

**Syntax**

- `BEGIN TRANSACTION` or `BEGIN`
- `COMMIT`
- `ROLLBACK`

**Scope**

- Lock scope: entire database or per-table (implementation choice)
- All operations between BEGIN and COMMIT/ROLLBACK are atomic
- Implicit rollback on error or connection close

**Example**

```sql
BEGIN TRANSACTION;
INSERT INTO Users (Id, Name) VALUES ('1', 'Alice');
UPDATE Orders SET Status = 'Shipped' WHERE UserId = '1';
COMMIT;
```

---

## Phase 6 — Programmability

### 6.1 Views

**Syntax**

```sql
CREATE VIEW ViewName AS
SELECT column1, column2 FROM Table1 JOIN Table2 ON ...;
```

**Storage**

- Views/ folder; one folder per view
- `<ViewName>/definition.txt` or similar — Stores the SELECT text
- Metadata in ~System/ for view definitions

**Behavior**

- Views are virtual; no stored data
- SELECT from view executes the stored query
- Depends on Phase 4 features (JOINs, aggregates, etc.)

### 6.2 Stored Procedures

**Syntax**

```sql
CREATE PROCEDURE ProcedureName
AS
  INSERT INTO Log (Message) VALUES ('Started');
  UPDATE Users SET LastLogin = GETDATE();
  -- etc.
```

**Execution**

- `EXEC ProcedureName` or `EXECUTE ProcedureName`
- Parameters: optional Phase 6 extension (`@param type`)

**Storage**

- Procedures/ folder; one file per procedure
- `<ProcedureName>.txt` — Procedure body (SQL text)

**Behavior**

- Multi-statement; requires Transactions (Phase 5)
- Procedures can call other procedures (optional)

### 6.3 Functions

**Syntax**

```sql
CREATE FUNCTION FunctionName (@param1 type, @param2 type)
RETURNS type
AS
  RETURN (SELECT expression);
```

**Storage**

- Functions/ folder; one file per function
- `<FunctionName>.txt` — Function definition

**Scope**

- Scalar functions only in initial Phase 6
- Table-valued functions as future extension

**Example**

```sql
SELECT Id, dbo.FormatName(FirstName, LastName) FROM Users;
```

---

## Storage Impact

Views/, Procedures/, and Functions/ folders are already defined in [02-storage-format.md](../architecture/02-storage-format.md). Phase 6 populates them:

| Folder | Contents |
|--------|----------|
| Views/ | One folder per view; definition file |
| Procedures/ | One file per procedure; procedure body |
| Functions/ | One file per function; function definition |

~System/ stores metadata for all three (names, definitions, dependencies).

---

## Implementation Outline

| Feature | High-level approach |
|---------|----------------------|
| **JOINs** | Extend parser for JOIN clause; engine builds join plan; nested-loop or hash join executor |
| **Aggregates** | Parser recognizes aggregate functions; engine aggregates during scan; GROUP BY partitions rows |
| **ORDER BY** | Sort step after scan; in-memory sort; streaming for large result sets (future) |
| **GROUP BY** | Hash or sort-based grouping; emit one row per group |
| **Subqueries** | Parser produces subquery AST; engine executes subquery per outer row (correlated) or once (uncorrelated) |
| **ALTER TABLE** | Schema store updates; data migration for ADD/DROP; index rebuild |
| **Transactions** | Transaction context; lock scope; commit/rollback flushes or discards changes |
| **Views** | View definition store; SELECT from view expands to underlying query |
| **Stored Procedures** | Procedure body store; EXEC parses and runs statements in transaction |
| **Functions** | Function definition store; invoke in expression context |

---

## Reference

- [01_Initial_Creation.md](01_Initial_Creation.md) — Phase 1–3 specification
- [02-storage-format.md](../architecture/02-storage-format.md) — Directory layout
- [03-sql-subset.md](../architecture/03-sql-subset.md) — Supported syntax
