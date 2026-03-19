# Performance and Efficiency

## Overview

SQL.txt prioritizes **speed**, **memory efficiency**, and **durability** alongside correctness. For each feature and I/O path, implementations should minimize I/O, avoid unnecessary memory allocation, and ensure atomic writes. Breaking conventional coding patterns is acceptable when it yields measurable gains in these areas.

### Knuth-Style Principle

Never implement the most straightforward approach for data-focused tasks. Think like a data or software scientist: structure all data management, building, linking, searching, and operations for maximum efficiency. Design for speed and efficiency from the start—choose algorithms and structures (e.g., binary search, B-trees or other ordered structures, incremental updates, STOC for indexes) that scale well.

### Data structure strategy (logical vs physical)

- **Logical model** stays relational: rows, keys, and constraints as seen by SQL and applications.
- **Physical representation** (in memory and on disk) should use whatever layout yields the fastest **reads and writes** and lowest I/O volume: sorted runs, trees, page-like segments, append-then-compact, hash tables with overflow—**not** necessarily a single linear row stream on disk.
- **Text backend** remains human-inspectable where required; prefer a clear on-disk encoding and `storageFormatVersion` when layouts evolve. **Binary backend** may use more compact or seek-friendly layouts sooner.
- Prefer structures that support **O(log n)** keyed access (or better) when indexes grow; avoid relying on “scan the whole file” in hot paths once Phase 3.5 index ordering is in place.

## Principles

### Speed

- Minimize I/O: Prefer streaming over full-file loads; use buffering where appropriate.
- Avoid full-file reads for large data: Use line-by-line or chunk-based reads instead of `ReadAllTextAsync` / `ReadAllLinesAsync` when files may grow large.
- Prefer append-only for INSERT: Already used; avoid rewriting entire files for single-row inserts.

### Memory

- Prefer O(1) or O(row) memory: Avoid loading entire tables when streaming is possible.
- Use `IAsyncEnumerable` with true streaming: Yield rows as they are read, not after full materialization.
- Avoid `string.Join` for very large row sets: Use `StreamWriter` or `StringBuilder` with bounded growth.

### Durability

- Atomic writes: Write to a temporary file, then `File.Move` (overwrite) to replace. No truncate-then-write; no partial file states visible to readers.
- Copy-on-write for UPDATE/DELETE: Stream read → stream write to temp → atomic rename.
- No partial writes that leave data corrupted.

### Trade-offs

- Breaking conventional patterns (e.g., fewer abstractions, more direct I/O) is acceptable when it yields measurable speed or memory gains.
- Document any such trade-offs and the rationale.

## Text-File Constraints

SQL.txt uses human-readable text files. These constraints inform efficient implementation:

- **Fixed-width rows** (Phase 1): Enable seekable offsets for in-place updates when row length is unchanged. Future optimization opportunity.
- **Line-delimited format**: Enables line-by-line streaming via `StreamReader` / `ReadLine` for O(1) memory per row.
- **Append-only for INSERT**: Use **true append** at the filesystem layer (`AppendAllText` / append `FileStream`) for growing data and index files. **Do not** implement append as read-the-entire-file plus `WriteAllText` of the combined content (that is O(n²) bytes written for n rows). Caching layers must append to the inner storage and extend in-memory cache by appending bytes, not by rewriting whole files per row.
- **Copy-on-write for UPDATE/DELETE**: Stream read → stream write to temp file → atomic rename. Avoids loading entire table into memory.

## Recommended Patterns

### Read

- **SELECT / scan**: `IAsyncEnumerable` with true streaming (one line at a time from `StreamReader`), not per-shard full load.
- **Schema / metadata**: Small files; `ReadAllTextAsync` is acceptable.
- **Large files**: Consider `MemoryMappedFile` for read-only scans when files exceed a configurable threshold (e.g., 100 MB).

### Write

- **INSERT**: Append-only via **true** `AppendAllTextAsync` / `AppendAllBytesAsync` on the backing accessor, or `StreamWriter` in append mode. Wrappers (e.g. LRU file cache) must not turn each append into a full-file rewrite.
- **Multi-row INSERT:** Batched validation; batched or chunked writes per shard and per index file (Phase 3.5). See [Phase3_5_Storage_Efficiency_Plan.md](../plans/Phase3_5_Storage_Efficiency_Plan.md).
- **UPDATE / DELETE**: Stream-in, stream-out to temp file; `File.Move` for atomicity. Avoid building full content in memory.
- **String building**: Use `StringBuilder` for repeated concatenation; avoid `string + string` in loops.

### Atomicity

- Writes that replace file content: Write to `.tmp`, then `File.Move` (overwrite) to replace.
- No truncate-then-write; no partial file states visible to readers.

## Interface Extensions

Implemented:

- **IFileSystemAccessor**: `OpenReadStreamAsync` and `OpenWriteStreamAsync` enable chunked/streaming reads and writes for large files. `ReadLinesAsync` provides streaming for text.
- **Binary backend**: Uses `BinaryRecordStreamHelper` with ArrayPool buffers for O(record) memory during scans; shard split and transform use two-pass streaming.

## INSERT Efficiency

- **STOC update:** Only on shard split and rebalance (per [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md)); not on every append to existing shards.
- **Batch RowId allocation:** For multi-row INSERT, allocate RowIds in a single read/write cycle.
- **Storage backend caching:** Cache resolved backend per database path; avoid repeated manifest reads.
- See [Phase3_5_Storage_Efficiency_Plan.md](../plans/Phase3_5_Storage_Efficiency_Plan.md) for batched INSERT, append I/O, and sorted indexes.

## Index and Statistics Design

- **Index lookup:** Must use **O(log n) binary search** on sorted index files. Sequential scan is non-compliant. See [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md).
- **SELECT with index:** When an index exists for the WHERE predicate, avoid full table scan. Use index lookup to get RowIds, then fetch only matching rows via `ReadRowsByRowIdsAsync` or equivalent. Do not stream all rows and filter in memory.
- **Index STOC:** Shard Table of Contents enables O(affected rows) maintenance on shard split; avoid full index rebuild. See [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md).
- **Statistics-ready:** Index format (sorted) and ~System metadata slots reserved for Phase 7 statistics. See [adr-006-statistics-design.md](../decisions/adr-006-statistics-design.md).

## In-Memory Caching and Load-Into-Memory Mode

### Layered In-Memory Approach

Three complementary layers reduce disk I/O:

| Layer | Scope | Benefit |
|-------|-------|---------|
| **Index + metadata cache** | Per-table, per-database | Eliminates repeated index/metadata file reads via `CachingIndexStore` and `CachingMetadataStore`. When cached, `CachingIndexStore` uses O(log n) binary search for lookups. |
| **CachingFileSystemAccessor** | File-level LRU cache | Enabled by default. Keeps hot shards and indexes in memory; configurable `maxCachedBytes` (default 64 MB); write-through. Set `useFileSystemCache: false` for memory-constrained workloads. |
| **Load-into-memory mode** | Full database | Load filesystem DB into `MemoryFileSystemAccessor`; operate entirely in RAM; optional flush on exit |

### Load-Into-Memory Mode

- **CLI:** `--memory` loads the database into memory; `--persist` flushes changes back to disk on exit.
- **API:** `DatabaseEngine.LoadIntoMemoryAsync(path)` returns an engine with in-memory storage; call `FlushToDiskAsync` when done.
- **Use case:** Interactive or batch workloads where maximum speed is desired; optionally persist at the end.
- **Constraint:** Loading a large database consumes RAM. Consider a size check before load (e.g., warn if total DB size > 500 MB).

### Caching Correctness

- **Index/Metadata cache:** Write-through; cache always consistent with disk after write.
- **CachingFileSystemAccessor:** Write-through; cache updated on write.
- **Load-into-memory:** All ops in memory; durability only on flush (exit or explicit call).

## Reference

- [02-storage-format.md](02-storage-format.md) — On-disk layout, row format
- [06-durability-and-sharding.md](06-durability-and-sharding.md) — Sharding, error handling
- [Efficiency_Audit_Methodology.md](../plans/Efficiency_Audit_Methodology.md) — Audit process for existing code
- [Phase3_5_Storage_Efficiency_Plan.md](../plans/Phase3_5_Storage_Efficiency_Plan.md) — Phase 3.5 ingest and index efficiency
