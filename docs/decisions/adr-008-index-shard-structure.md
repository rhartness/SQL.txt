# ADR-008: Index Shard Structure

## Status

Accepted

## Context

When table data is sharded across multiple files, indexes must reference rows by shard and position. On shard split, updating every index entry for every row would be O(n) and inefficient. A Shard Table of Contents (STOC) and incremental index maintenance enable O(affected rows) updates.

## Decision

### Shard Table of Contents (STOC)

File: `<TableName>_STOC.txt` — one line per shard

```
ShardId|MinRowId|MaxRowId|FilePath|RowCount
0|1|1000|Users.txt|1000
1|1001|2000|Users_1.txt|1000
```

- Enables range-based lookups and efficient shard split maintenance
- Updated when shards are created, split, or rebalanced
- Not updated on every append to existing shards; only when shards are created (split), split, or rebalanced

### Index Format (Multi-Shard)

```
<TableName>_INX_<Col>_N.txt
Value|ShardId|_RowId
```

- Lookup: Binary search on Value → get (ShardId, _RowId) → fetch row from shard
- Index entries are sorted by Value for O(log n) lookup

### Shard Split Maintenance

On shard split:

1. Create new shard file; move rows via stream
2. Update STOC: add new shard line; update original shard's MaxRowId and RowCount
3. **Incremental index update:** Add new index entries only for rows moved to the new shard. Do **not** rebuild entire index.
4. Optionally remove entries for rows that left the original shard (or mark obsolete if using append-only index with compaction later)

### Avoid Full Rebuild

- Never scan entire table to rebuild index on shard split
- Index maintenance is O(rows moved), not O(total rows)

## Consequences

- Index store interface: `AddIndexEntryAsync(value, shardId, rowId)`; `LookupByValueAsync` returns `(ShardId, RowId)[]`
- STOC must be created and maintained in Phase 2
- Binary search requires sorted index files; append-and-sort or merge on compaction

## Implementation Status

**Index lookup must use O(log n) binary search.** The ADR specifies "Binary search on Value → get (ShardId, _RowId)" for index lookup. A sequential scan of the index file is **non-compliant** with this decision and with the efficiency requirements in [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md).

**Current implementation (Phase 3.5):** Index files are maintained **sorted by key** (line order = sort order of full index lines). `IndexStore` uses **binary search** on the sorted line list for `LookupByValueAsync` and `ContainsKeyAsync`. `CachingIndexStore` mirrors the same invariant when the cache is loaded from disk. Legacy databases with unsorted index files are **normalized to sorted order** when merged on write or when first read for lookup (one-time cost). Optional future work: fixed-width or paged binary index segments for larger scale; logical behavior unchanged.
