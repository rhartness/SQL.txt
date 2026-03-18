# ADR-006: Statistics Design

## Status

Accepted

## Context

SQL Server and other RDBMSs use statistics (histograms, cardinality estimates) to drive query optimization. SQL.txt will eventually implement a robust statistics system similar to SQL Server. Design decisions now must enable efficient implementation later without requiring major refactoring.

## Decision

### Phase 7: Statistics Phase

A dedicated **Phase 7** will implement statistics:

- `CREATE STATISTICS` on table columns
- Histogram storage (value distribution, step bounds)
- Cardinality estimation for query planning
- Integration with query optimizer (when present)

### Design Hooks (Phases 1–6)

1. **Metadata slots:** Reserve columns in `~System` for future statistics metadata (row count, distinct value count, min/max, histogram step count).

2. **File layout:** Plan `~System/Statistics/<TableName>_<ColumnNames>.txt` for histogram storage. Format TBD in Phase 7.

3. **Index format:** Index files will be sorted (or sortable) to support efficient histogram building via sampling or full scan.

4. **No implementation until Phase 7** — design only; no code changes for statistics in earlier phases beyond metadata placeholders.

### SQL Server–Style Features (Phase 7 Target)

- Histogram with step bounds (e.g., max 200 steps)
- `sys.stats`-like metadata
- Automatic statistics creation on indexed columns (optional)
- Manual `CREATE STATISTICS` and `DROP STATISTICS`

## Consequences

- Index design in Phase 2 must consider sort order for future statistics
- ~System schema should have extensible metadata structure
- Phase 7 plan stub created; full implementation deferred
