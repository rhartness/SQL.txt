# Efficiency Improvements Plan

## Purpose

Address identified bottlenecks in the SQL.txt engine that cause slow multi-row INSERT and sharded table operations. Align implementation with documented efficiency principles in [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md) and [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md).

**Master plan:** [Phase3_5_Storage_Efficiency_Plan.md](Phase3_5_Storage_Efficiency_Plan.md) (Phase 3.5). This document remains the detailed checklist for INSERT/STOC/index items; §4 is **in scope** for Phase 3.5.

## Reference

- [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md)
- [Efficiency_Audit_Methodology.md](Efficiency_Audit_Methodology.md)
- [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md)
- [Phase2_Implementation_Plan.md](Phase2_Implementation_Plan.md)

---

## Identified Bottlenecks

| Bottleneck | Impact | Spec Alignment |
|------------|--------|-----------------|
| BuildAndWriteStocAsync on every append to shards 1+ | O(n²) — full shard scan per row | ADR-008: STOC updated on split/rebalance only |
| RowId read/write per row | 2 file ops per row | Not specified; batch allocation improves |
| Storage backend resolution per append | Manifest read per row | Not specified; cache per database |
| Index appends (4 per row) | 4 file appends per row | Batch append reduces I/O; deferred |

---

## Implementation Tasks

### 1. STOC Update: Split/Rebalance Only

**Requirement:** Per ADR-008, STOC is "Updated when shards are created, split, or rebalanced."

**Current behavior:** `AppendRowAsync` calls `BuildAndWriteStocAsync` whenever `shardIndex >= 1`, causing a full scan of all shards on every append to shards 1+.

**Change:** Remove the `BuildAndWriteStocAsync` call from `AppendRowAsync`. STOC is already updated at the end of `SplitShardAsync` and in `StreamTransformRowsAsync` (rebalance path). STOC may be stale for append-only growth of existing shards until next split/rebalance; acceptable because STOC is not yet used for reads.

**Files:** `TableDataStore.cs`

---

### 2. Batch RowId Allocation

**Requirement:** For multi-row INSERT, allocate RowIds in a single read/write cycle instead of per row.

**Change:**
- Add `GetNextRangeAndIncrementAsync(databasePath, tableName, count)` to `IRowIdSequenceStore`
- Implement in `RowIdSequenceStore`: read once, increment by count, write once
- In `ExecuteInsertAsync`, when `cmd.ValueRows.Count > 1`, call batch method once, assign sequential RowIds to rows

**Files:** `IRowIdSequenceStore.cs`, `RowIdSequenceStore.cs`, `DatabaseEngine.cs`

---

### 3. Storage Backend Caching

**Requirement:** Avoid reading manifest.json on every `AppendRowAsync` call.

**Change:** Add `CachingStorageBackendResolver` that wraps `StorageBackendResolver` and caches `(databasePath) -> IStorageBackend` per database. TableDataStore receives resolver; first resolve caches result for that database path.

**Files:** New `CachingStorageBackendResolver.cs`, `TableDataStore.cs`, `DatabaseEngine.cs` (wire up)

---

### 4. Batch Index Appends (Phase 3.5)

**Requirement:** Reduce index file I/O for multi-row INSERT via `AddIndexEntriesAsync` (and related batched validation). On-disk index files remain **sorted by key** after writes (see ADR-008).

**Status:** **Active** — tracked under [Phase3_5_Storage_Efficiency_Plan.md](Phase3_5_Storage_Efficiency_Plan.md).

---

### 5. Index O(log n) Lookup (Phase 3.5)

**Requirement:** Sorted on-disk index lines with **binary search** for `LookupByValueAsync` / `ContainsKeyAsync` (ADR-008). Legacy unsorted files are normalized on read/write merge paths.

**Status:** **Active** under Phase 3.5; see [Phase3_5_Storage_Efficiency_Plan.md](Phase3_5_Storage_Efficiency_Plan.md) M3.

---

## Maintenance

- **Efficiency_Audit_Methodology.md:** Run audit before new phases; add efficiency tasks to phase plans.
- **10-performance-and-efficiency.md:** Reference this plan for INSERT and STOC behavior.
- **New features:** Check [Efficiency_Audit_Methodology.md](Efficiency_Audit_Methodology.md) checklist; avoid per-row full scans.

---

## Acceptance Criteria

- [x] STOC updated only on shard split and rebalance; not on every append to shards 1+
- [x] Multi-row INSERT uses batch RowId allocation (1 read/write for N rows)
- [x] Storage backend resolved once per database per session (cached)
- [x] Manual sharding test (500 rows) — typical ~300 ms after Phase 3.5 (environment-dependent)
- [x] Manual sharding-varchar test passes with variable-width rows (incl. `StreamTransformRowsAsync` input snapshot fix)
- [x] All existing tests pass
