# Plan F: CTE Phase — Common Table Expressions (SQL:2023)

**Status:** Pending  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan E complete  
**Spec Reference:** ISO/IEC 9075-2:2023 SQL/Foundation — Clause 7.16 (query expression)

**Reference:** [docs/specifications/02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md), [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)

---

## SQL:2023 Compliance

| Feature ID | Name | Status |
|------------|------|--------|
| — | WITH clause (non-recursive CTE) | Planned |
| — | WITH RECURSIVE (recursive CTE) | Planned |
| T133 | Enhanced cycle mark values | Enhancement |

**Full feature list:** See [01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md)

## Compliance Checklist

- [ ] WITH cte AS (SELECT ...) SELECT ...
- [ ] WITH RECURSIVE cte AS (anchor UNION ALL recursive) SELECT ...
- [ ] Multiple CTEs
- [ ] T133 cycle detection (optional enhancement)

---

## Scope

- WITH clause (non-recursive)
- Recursive CTE (UNION ALL anchor + recursive)
- SQL:2023 compliant

---

## Wave 1: Parser

### 1.1 WITH Clause Syntax

- `WITH cte_name AS (SELECT ...) SELECT ... FROM cte_name`
- Multiple CTEs: `WITH cte1 AS (...), cte2 AS (...) SELECT ...`
- Column list: `WITH cte (col1, col2) AS (...) SELECT ...`

### 1.2 Recursive CTE

- `WITH RECURSIVE cte AS (anchor SELECT UNION ALL recursive SELECT) SELECT ...`
- Anchor: base case
- Recursive: references cte; termination condition in WHERE

---

## Wave 2: Execution Engine

### 2.1 Non-Recursive CTE

- Evaluate CTE query once; materialize result (or inline if small)
- Use result in outer query

### 2.2 Recursive CTE

- Iterate: compute anchor, then recursive part until no new rows
- UNION ALL semantics; avoid duplicates in iteration
- Cycle detection (T133) if in scope
- Depth limit for safety

---

## Wave 3: Integration

### 3.1 CTE with JOINs, Aggregates

- CTE can be used in JOINs, subqueries
- Ensure correct scoping and evaluation order

---

## Wave 4: Tests and Documentation

### 4.1 Unit and Integration Tests

- Non-recursive: single and multiple CTEs
- Recursive: factorial, tree traversal, graph reachability
- Edge cases: empty anchor, cycle detection

### 4.2 Documentation

- Update 11-sql2023-mapping.md: WITH, recursive CTE
- Update README Roadmap
- Examples for CLI, WASM, Embedding

---

## Plan Execution Rules (Apply on Completion)

1. Update README Roadmap table
2. Update 11-sql2023-mapping.md
3. Update CLI reference, Getting Started
4. Provide examples for CLI, WASM, Embedding

---

## Deliverable

WITH clause and recursive CTE. SQL:2023 compliant. Enterprise-grade execution with cycle detection and depth limits.
