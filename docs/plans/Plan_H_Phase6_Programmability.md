# Plan H: Phase 6 — Programmability (SQL:2023)

**Status:** Pending  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan G complete  
**Spec Reference:** ISO/IEC 9075-2:2023 SQL/Foundation — Clause 4 (Schema), 11 (Routines)

**Reference:** [docs/specifications/02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md), [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)

---

## SQL:2023 Compliance

| Feature ID | Name | Status |
|------------|------|--------|
| Core | CREATE VIEW | Planned |
| Core | CREATE PROCEDURE | Planned |
| Core | CREATE FUNCTION (scalar) | Planned |

**Full feature list:** See [01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md)

## Compliance Checklist

- [ ] CREATE VIEW name AS SELECT ...
- [ ] DROP VIEW
- [ ] CREATE PROCEDURE name AS ...
- [ ] EXEC / CALL procedure
- [ ] CREATE FUNCTION name RETURNS type AS ...
- [ ] Scalar function invocation in expressions

---

## Scope

- CREATE VIEW
- CREATE PROCEDURE, EXEC
- CREATE FUNCTION (scalar)

---

## Wave 1: Views

### 1.1 Parser

- `CREATE VIEW v AS SELECT ...;`
- `DROP VIEW v;`

### 1.2 Storage

- Store view definition in Views/ folder
- Schema: view name, SELECT text (or parsed AST)

### 1.3 Execution

- SELECT from view: expand view definition; execute underlying SELECT
- Support views over views (recursive expansion with depth limit)
- Updatable views (optional): if view is simple enough (single table, no aggregates, etc.)

---

## Wave 2: Stored Procedures

### 2.1 Parser

- `CREATE PROCEDURE p (params) AS BEGIN ... END;`
- `EXEC p(args);` or `CALL p(args);`

### 2.2 Storage

- Store procedure in Procedures/ folder
- Body: SQL statements (or parsed AST)

### 2.3 Execution

- EXEC: parse and execute procedure body
- Support parameters (IN, OUT, INOUT if in scope)
- Multi-statement execution; transaction scope per procedure or configurable

---

## Wave 3: Functions

### 3.1 Parser

- `CREATE FUNCTION f (params) RETURNS type AS BEGIN ... RETURN expr; END;`
- Use in SELECT: `SELECT f(col) FROM t;`

### 3.2 Storage

- Store function in Functions/ folder

### 3.3 Execution

- Invoke function for each row (or optimize when possible)
- RETURN expression evaluation
- Scalar functions only in initial scope

---

## Wave 4: Tests and Documentation

### 4.1 Unit and Integration Tests

- Create/drop view; SELECT from view
- Create procedure; EXEC
- Create function; use in SELECT
- Both text and binary backends

### 4.2 Documentation

- Update 11-sql2023-mapping.md: Views, procedures, functions
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

CREATE VIEW, CREATE PROCEDURE, CREATE FUNCTION. EXEC for procedures. Scalar functions in SELECT. SQL:2023 compliant. Enterprise-grade storage and execution.
