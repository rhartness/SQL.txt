# Phase 3.5 — Storage & Ingest Efficiency

## Purpose

Deliver **correct I/O semantics** (true append, no read–modify–write growth on append), **batched multi-row INSERT** (validation, data, indexes), and **on-disk index ordering** aligned with [ADR-008](../decisions/adr-008-index-shard-structure.md) before Phase 4 query work. Phase 3.5 is an **implementation milestone** (engine/storage), not a new SQL:2023 clause.

## References

- [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md)
- [Efficiency_Improvements_Plan.md](Efficiency_Improvements_Plan.md)
- [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md)
- [02-storage-format.md](../architecture/02-storage-format.md)

## Principles

- **Logical** model: relational rows, keys, constraints (unchanged).
- **Physical** layout: any structure that optimizes read/write/latency and I/O volume (sorted runs, in-memory trees, paged layouts), with **format versioning** and **text-backend inspectability** where required.

## StreamTransform / rebalance (correctness)

`StreamTransformRowsAsync` must **enumerate input shard files once** from a snapshot taken **before** any output flush. Output writes create `Table_1`, `Table_2`, …; scanning the directory incrementally would treat those as extra inputs and never terminate (fixed in Phase 3.5).

## Milestones

### M1 — Filesystem append (completed in this phase)

- `CachingFileSystemAccessor.AppendAllTextAsync` / `AppendAllBytesAsync` call **`_inner` true append**; update LRU cache by **appending** to cached bytes (cache miss: append only, optionally extend cache from disk read only when policy requires consistency).
- **Files:** `CachingFileSystemAccessor.cs`

### M2 — Batched multi-row INSERT

- **`ExecuteInsertAsync`** when `ValueRows.Count > 1`: batch PK/unique duplicate checks in memory; batch FK checks using parent key set loaded once per referenced table; reuse batch RowId allocation.
- **Data:** append rows per shard; prefer batched bytes/text per shard where practical.
- **Indexes:** `IIndexStore.AddIndexEntriesAsync` — many entries, **one or few** disk operations per index file per statement; `CachingIndexStore` updates line cache in bulk.
- **Files:** `DatabaseEngine.cs`, `IIndexStore.cs`, `IndexStore.cs`, `CachingIndexStore.cs`, tests.

### M3 — On-disk sorted index (ADR-008)

- After applying a batch of new index lines, **merge with existing file content**, **sort lines by key prefix** (`Value` before first `|`), write atomically (temp + move) so **sequential file order matches sort order** and on-disk lookup can use **binary search without full string load** (stream line-by-line into sorted list for moderate files, or binary search on line offsets after load — implementation in `IndexStore`).
- **Lookup/Contains:** use binary search on sorted lines (load file once per operation or use cache).
- **Files:** `IndexStore.cs`, `CachingIndexStore.cs`, ADR-008, tests.

### M4 — Documentation & gates

- README, AGENTS, rules, roadmap, efficiency docs updated; manual tests pass.

## Acceptance criteria

- [x] No O(n²) growth from cached append on INSERT-heavy workloads.
- [x] Multi-row `INSERT ... VALUES (...), (...)` uses batched index writes and batched constraint checks where applicable.
- [x] Index files remain **sorted by key** after batch application; `LookupByValueAsync` / `ContainsKeyAsync` use binary search on sorted lines.
- [x] `sharding`, `sharding-varchar`, `concurrency` manual tests pass.
- [x] `dotnet test` passes.

## Manual tests

```bash
dotnet run --project src/SqlTxt.ManualTests -- sharding
dotnet run --project src/SqlTxt.ManualTests -- sharding-varchar
dotnet run --project src/SqlTxt.ManualTests -- concurrency
```

## Doc checklist (on completion)

- [ ] README roadmap row Phase 3.5
- [ ] AGENTS.md + `.cursor/rules/sql-txt-project-overview.mdc`
- [ ] `docs/roadmap/00-sql2023-compliance-roadmap.md`
- [ ] `docs/plans/Efficiency_Improvements_Plan.md` §4 active
- [ ] `docs/architecture/10-performance-and-efficiency.md` data-structure section
- [ ] `docs/decisions/adr-008-index-shard-structure.md` implementation status
- [ ] `docs/architecture/02-storage-format.md`, `05-documentation-standards.md`, `Efficiency_Audit_Methodology.md`
- [ ] Optional: `01-sql2023-feature-registry.md` footnote for multi-row batched path

## Out of scope

- T-SQL–style `BULK INSERT` from external file.
- Multi-statement transactions (Phase 5); document best-effort durability until then.
