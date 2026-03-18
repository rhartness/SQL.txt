# Performance and Efficiency

## Overview

SQL.txt prioritizes **speed**, **memory efficiency**, and **durability** alongside correctness. For each feature and I/O path, implementations should minimize I/O, avoid unnecessary memory allocation, and ensure atomic writes. Breaking conventional coding patterns is acceptable when it yields measurable gains in these areas.

### Knuth-Style Principle

Never implement the most straightforward approach for data-focused tasks. Think like a data or software scientist: structure all data management, building, linking, searching, and operations for maximum efficiency. Design for speed and efficiency from the start—choose algorithms and structures (e.g., binary search, incremental updates, STOC for indexes) that scale well.

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
- **Append-only for INSERT**: Already used; efficient for single-row and batch inserts.
- **Copy-on-write for UPDATE/DELETE**: Stream read → stream write to temp file → atomic rename. Avoids loading entire table into memory.

## Recommended Patterns

### Read

- **SELECT / scan**: `IAsyncEnumerable` with true streaming (one line at a time from `StreamReader`), not per-shard full load.
- **Schema / metadata**: Small files; `ReadAllTextAsync` is acceptable.
- **Large files**: Consider `MemoryMappedFile` for read-only scans when files exceed a configurable threshold (e.g., 100 MB).

### Write

- **INSERT**: Append-only via `AppendAllTextAsync` or `StreamWriter` with append mode. Already efficient.
- **UPDATE / DELETE**: Stream-in, stream-out to temp file; `File.Move` for atomicity. Avoid building full content in memory.
- **String building**: Use `StringBuilder` for repeated concatenation; avoid `string + string` in loops.

### Atomicity

- Writes that replace file content: Write to `.tmp`, then `File.Move` (overwrite) to replace.
- No truncate-then-write; no partial file states visible to readers.

## Interface Extensions

Future interface changes to support efficiency:

- **IFileSystemAccessor**: Add `OpenReadStreamAsync`, `OpenWriteStreamAsync`, or `ReadLinesAsync` (streaming `IAsyncEnumerable<string>`) for large-file scenarios.
- **ITableDataStore**: Ensure `ReadRowsAsync` uses true streaming internally (line-by-line); consider `StreamRowsAsync` if a different contract is needed.

## INSERT Efficiency

- **STOC update:** Only on shard split and rebalance (per [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md)); not on every append to existing shards.
- **Batch RowId allocation:** For multi-row INSERT, allocate RowIds in a single read/write cycle.
- **Storage backend caching:** Cache resolved backend per database path; avoid repeated manifest reads.
- See [Efficiency_Improvements_Plan.md](../plans/Efficiency_Improvements_Plan.md) for implementation details.

## Index and Statistics Design

- **Index STOC:** Shard Table of Contents enables O(affected rows) maintenance on shard split; avoid full index rebuild. See [adr-008-index-shard-structure.md](../decisions/adr-008-index-shard-structure.md).
- **Statistics-ready:** Index format (sorted) and ~System metadata slots reserved for Phase 7 statistics. See [adr-006-statistics-design.md](../decisions/adr-006-statistics-design.md).

## Reference

- [02-storage-format.md](02-storage-format.md) — On-disk layout, row format
- [06-durability-and-sharding.md](06-durability-and-sharding.md) — Sharding, error handling
- [Efficiency_Audit_Methodology.md](../plans/Efficiency_Audit_Methodology.md) — Audit process for existing code
- [Efficiency_Improvements_Plan.md](../plans/Efficiency_Improvements_Plan.md) — INSERT and STOC efficiency fixes
