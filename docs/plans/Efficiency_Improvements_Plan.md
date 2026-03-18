# Efficiency Improvements Plan

## Purpose

Address identified bottlenecks in the SQL.txt engine that cause slow multi-row INSERT and sharded table operations. Align implementation with documented efficiency principles in [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md) and [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md).

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

### 4. Batch Index Appends (Deferred)

**Requirement:** Reduce index file I/O for multi-row INSERT.

**Status:** Deferred — implement STOC fix and RowId batching first; measure improvement. Add if needed.

---

### 5. Index O(log n) Lookup (Future)

**Requirement:** Phase 2 specifies sorted indexes for O(log n) binary search. Current `ContainsKeyAsync` does O(n) scan.

**Status:** Deferred. Document in Phase 2 Efficiency Notes; implement when index format is finalized.

---

## Maintenance

- **Efficiency_Audit_Methodology.md:** Run audit before new phases; add efficiency tasks to phase plans.
- **10-performance-and-efficiency.md:** Reference this plan for INSERT and STOC behavior.
- **New features:** Check [Efficiency_Audit_Methodology.md](Efficiency_Audit_Methodology.md) checklist; avoid per-row full scans.

---

## Acceptance Criteria

- [ ] STOC updated only on shard split and rebalance; not on every append to shards 1+
- [ ] Multi-row INSERT uses batch RowId allocation (1 read/write for N rows)
- [ ] Storage backend resolved once per database per session (cached)
- [ ] Manual sharding test (500 rows) runs significantly faster (e.g., < 2s vs ~10s)
- [ ] Manual sharding-varchar test passes with variable-width rows
- [ ] All existing tests pass
