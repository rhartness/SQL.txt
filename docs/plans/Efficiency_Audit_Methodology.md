# Efficiency Audit Methodology

## Purpose

A repeatable process for scanning existing SQL.txt code and planning efficiency improvements. Use this methodology before implementing new phases, when adding features, and after significant I/O changes.

**Phase 3.5 gate:** Before starting Phase 4 query features that depend on fast INSERT or index paths, complete or explicitly defer items in [Phase3_5_Storage_Efficiency_Plan.md](Phase3_5_Storage_Efficiency_Plan.md) (true append, batched multi-row INSERT, sorted indexes). Run this audit on any new storage PR.

**Phase 4 gate:** Any PR that adds JOIN, ORDER BY, GROUP BY, or subquery execution must be checked against [Phase4_Implementation_Plan.md](Phase4_Implementation_Plan.md) and the relevant **Phase4_0x_*.md** sub-plan: bounded work memory, spill where specified, no default full materialization + `List.Sort` as the only sort path, and join algorithm choice documented (hash / merge / index nested loop). Extend this audit’s checklist to **query executor** hot paths (per-row allocations, repeated subquery execution).

## Scan Targets

Audit the following:

1. **IFileSystemAccessor call sites** — `ReadAllTextAsync`, `ReadAllLinesAsync`, `WriteAllTextAsync`, `AppendAllTextAsync`
2. **ITableDataStore implementations and callers** — `ReadAllRowsWithStatusAsync`, `WriteAllRowsAsync`, `ReadRowsAsync`, `AppendRowAsync`
3. **Parser tokenization and parsing paths** — Token materialization, string handling
4. **String allocations in hot paths** — `string.Join`, `StringBuilder`, `string + string` in loops

## Knuth-Style Principle

Never implement the most straightforward approach for data-focused tasks. For each data structure, algorithm, or I/O path, consider:

- **Algorithmic complexity:** O(log n) over O(n) when applicable (e.g., binary search on sorted index)
- **Incremental updates:** On shard split, update only affected index entries; avoid full rebuild
- **Index structure:** STOC (Shard Table of Contents) for efficient multi-shard index maintenance per [adr-008](../decisions/adr-008-index-shard-structure.md)

## Checklist per Component

For each component that performs I/O or allocates large structures:

| Question | Notes |
|----------|-------|
| Does it load entire file into memory? | If yes, is the file expected to be large (table data, indexes)? |
| Does it stream? | If yes, is it true streaming (line-by-line) or per-chunk full load? |
| For writes: Is it atomic? | Does it use temp file + rename? |
| For UPDATE/DELETE: Could it use stream-in/stream-out? | Instead of full materialization; no full table load |
| Does it respect sharding? | Multi-shard read/write when applicable |
| STOC update frequency | Only on split/rebalance, not per append (see [adr-008](../decisions/adr-008-index-shard-structure.md), [Phase3_5_Storage_Efficiency_Plan.md](Phase3_5_Storage_Efficiency_Plan.md)) |
| Knuth-style? | Is the algorithm/structure optimal for scale? |
| **Index lookup O(log n)?** | Index lookup must use binary search, not sequential scan. See [adr-008](../decisions/adr-008-index-shard-structure.md). |
| **Index-driven SELECT?** | When index exists for WHERE, use index to fetch only matching rows; avoid full table scan. |
| **No full table load for UPDATE/DELETE?** | Use `StreamTransformRowsAsync` or equivalent; do not load entire table into memory. |

## Prioritization

| Priority | Components | Rationale |
|----------|------------|-----------|
| **High** | Table data reads/writes (TableDataStore, Engine UPDATE/DELETE) | Largest files; most likely to grow |
| **Medium** | Schema, metadata | Small files; lower priority |
| **Low** | Parser | SQL strings typically small; token count bounded |

## Steps for Subsequent Plans

1. **Before implementing a new phase** (e.g., Phase 2): Run the audit; add efficiency tasks to the phase plan.
2. **When adding a feature**: Check [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md); design for streaming and atomicity from the start.
3. **After changes**: Re-run the audit on modified components.

## Concrete Scan Commands

Use these patterns to locate candidates:

```
ReadAllTextAsync
ReadAllLinesAsync
WriteAllTextAsync
string.Join
ReadAllRowsWithStatusAsync
WriteAllRowsAsync
```

Search in `src/` and `tests/`; list files and line numbers; assess each against the checklist.

## Example Audit Output

| File | Line | Pattern | Assessment |
|------|------|---------|------------|
| TableDataStore.cs | 121 | ReadAllLinesAsync | Loads full shard; consider StreamReader line-by-line |
| TableDataStore.cs | 156 | WriteAllTextAsync | Full content write; consider stream-out; fix shard handling |
| DatabaseEngine.cs | 312 | ReadAllRowsWithStatusAsync | Full table load for UPDATE; consider stream-in/stream-out |

## Reference

- [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md) — Principles and recommended patterns
