# CTE Phase — Common Table Expressions Implementation Plan (Stub)

## Purpose

This phase implements Common Table Expressions (CTEs) via the WITH clause: non-recursive and recursive CTEs. Fully featured and functional per SQL:2023.

## Prerequisites

- Phase 4 complete (JOINs, aggregates, subqueries)
- Parser and engine support for subqueries

## Scope (Planned)

### Non-Recursive CTE

```sql
WITH cte AS (SELECT Id, Name FROM Users WHERE Active = 1)
SELECT * FROM cte WHERE Name LIKE 'A%';
```

### Recursive CTE

```sql
WITH RECURSIVE cte AS (
  SELECT Id, ParentId, 1 AS Level FROM Tree WHERE ParentId IS NULL
  UNION ALL
  SELECT t.Id, t.ParentId, c.Level + 1 FROM Tree t
  INNER JOIN cte c ON t.ParentId = c.Id
)
SELECT * FROM cte;
```

### Multiple CTEs

```sql
WITH a AS (SELECT 1 AS x), b AS (SELECT x + 1 AS y FROM a)
SELECT * FROM b;
```

## Design References

- [11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md) — CTE Phase section
- [03-sql-subset.md](../architecture/03-sql-subset.md) — CTE syntax

## Status

**Stub.** Full implementation plan to be generated when Phase 4 is complete.
