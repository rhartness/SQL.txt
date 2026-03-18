# Plan I: Phase 7 — Statistics

**Status:** Pending  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan H complete

**Reference:** [docs/decisions/adr-006-statistics-design.md](../decisions/adr-006-statistics-design.md), [docs/architecture/11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md)

---

## Scope

- CREATE STATISTICS
- Histograms
- Cardinality estimation
- Optimizer integration

---

## Wave 1: Statistics Storage

### 1.1 Metadata Slots

- Use reserved ~System slots per adr-006
- Store statistics per table/column(s)
- Format: histogram buckets, row count, distinct count

### 1.2 CREATE STATISTICS Parser

- `CREATE STATISTICS stat_name ON t (col1, col2);`
- `DROP STATISTICS stat_name;`

---

## Wave 2: Statistics Collection

### 2.1 Build Histogram

- Scan table (streaming); compute value distribution
- Build equi-depth or equi-width histogram
- Store in ~System

### 2.2 Update on Data Change

- Incremental update (optional) or full rebuild on demand
- Rebuild via explicit command or automatic on threshold

---

## Wave 3: Cardinality Estimation

### 3.1 Selectivity Estimation

- Use histogram for WHERE col = value, col > value, col BETWEEN
- Multi-column: combine single-column estimates or use joint histogram if available

### 3.2 Cost Model

- Estimate cost of table scan vs index lookup
- Use cardinality in join order selection (Phase 4 optimizer)

---

## Wave 4: Optimizer Integration

### 4.1 Use Statistics in Query Planning

- When choosing join order, access path (index vs scan)
- Prefer plans with lower estimated cost

---

## Wave 5: Tests and Documentation

### 5.1 Unit and Integration Tests

- CREATE STATISTICS; verify histogram stored
- Cardinality estimation accuracy
- Optimizer uses statistics

### 5.2 Documentation

- Update adr-006 with implementation notes
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

CREATE STATISTICS. Histogram storage. Cardinality estimation. Optimizer integration. Enterprise-grade statistics for query optimization.
