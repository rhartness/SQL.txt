# Plan E: Phase 4 — Query Enrichment (SQL:2023)

**Status:** Pending  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan D complete

**Reference:** [docs/specifications/02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md), [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)

---

## Scope

- JOINs (F401: INNER, LEFT; F406 FULL, F407 CROSS, F405 NATURAL)
- Aggregates (COUNT, SUM, AVG, MIN, MAX); T626 ANY_VALUE
- ORDER BY, GROUP BY, HAVING
- Subqueries (IN, EXISTS, scalar)
- Enterprise-grade: hash join / nested-loop; streaming aggregates

---

## Wave 1: Parser Extensions

### 1.1 JOIN Syntax

- INNER JOIN, LEFT [OUTER] JOIN, FULL OUTER JOIN (F406), CROSS JOIN (F407), NATURAL JOIN (F405)
- JOIN ON condition, JOIN USING (columns)

### 1.2 Aggregates

- COUNT(*), COUNT(column), SUM, AVG, MIN, MAX
- ANY_VALUE(column) — T626

### 1.3 ORDER BY, GROUP BY, HAVING

- ORDER BY col [ASC|DESC] [, ...]
- GROUP BY col [, ...]
- HAVING condition

### 1.4 Subqueries

- IN (subquery), EXISTS (subquery), scalar subquery in SELECT/WHERE

---

## Wave 2: Execution Engine

### 2.1 Join Execution

- Nested-loop join for small tables
- Hash join for larger tables (enterprise-grade)
- Support INNER, LEFT, FULL, CROSS, NATURAL

### 2.2 Aggregate Execution

- Streaming aggregates where possible
- GROUP BY with hash-based grouping
- HAVING filter after aggregation

### 2.3 Sort and Limit

- ORDER BY: external merge sort or in-memory for small result sets
- FETCH FIRST n ROWS (if in scope)

### 2.4 Subquery Execution

- IN: semi-join or hash lookup
- EXISTS: short-circuit when first match found
- Scalar: single-row subquery validation

---

## Wave 3: Query Optimizer (Basic)

### 3.1 Index Usage

- Use indexes for JOIN keys, WHERE, GROUP BY when applicable
- Cost-based or heuristic choice of join order

---

## Wave 4: Tests and Documentation

### 4.1 Unit and Integration Tests

- All JOIN types, aggregates, ORDER BY, GROUP BY, HAVING
- Subqueries: IN, EXISTS, scalar
- Both text and binary backends

### 4.2 Documentation

- Update 11-sql2023-mapping.md: F401, F405, F406, F407, T626
- Update README Roadmap
- Examples for all implementation types

---

## Plan Execution Rules (Apply on Completion)

1. Update README Roadmap table
2. Update 11-sql2023-mapping.md
3. Update CLI reference, Getting Started
4. Provide examples for CLI, WASM, Embedding

---

## Deliverable

Full Phase 4 query enrichment. JOINs, aggregates, ORDER BY, GROUP BY, HAVING, subqueries. Enterprise-grade execution (hash join, streaming aggregates). SQL:2023 F401, F405, F406, F407, T626.
