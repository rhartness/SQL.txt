# Durability, Sharding, and Error Handling

## Core Principle

Use **absolute best practices** for breaking up parts that might grow very large. As data is added to any data file (table data, indexes, multi-width field files), these files must be **shardable**.

## Sharding

### Database Default: defaultMaxShardSize

- **Default:** 20 MB (20,971,520 bytes) stored in `manifest.json` as `defaultMaxShardSize`
- **Override:** `CREATE DATABASE ... WITH (defaultMaxShardSize=...)`; `CREATE TABLE ... WITH (maxShardSize=...)`
- Per-table `MaxShardSize` overrides database default when specified

### Per-Table Parameter: MaxShardSize

Each table uses `MaxShardSize` (from table definition or database default). When a data file exceeds this limit, the engine creates a new shard file.

### Sharding Strategy

- **Table data files:** Shard when too large. Naming: `<TableName>_1.txt`, `<TableName>_2.txt`, etc.
- **Split strategy:** When a shard exceeds limit, create new shard and move approximately half (or tail) of rows via stream-in/stream-out. **Do not** rewrite the entire table.
- **Indexes (Phase 2+):** Format `Value|ShardId|_RowId`; Shard Table of Contents (STOC) tracks shard ranges. On split: incremental index update only (no full rebuild).
- **Metadata:** Keep metadata files small; shard if they grow (e.g., `~System` table data)

### Rebalance API

`RebalanceTableAsync(tableName)` — Scans all shards, redistributes rows to balance shard sizes, updates indexes and STOC. Exposed via Engine, Service (`POST /rebalance/{tableName}`), and CLI (`sqltxt rebalance --db ./Db --table Users`). See [adr-007-sharding-parameters.md](../decisions/adr-007-sharding-parameters.md).

**Implementation note (Phase 3.5):** `StreamTransformRowsAsync` takes a **snapshot of input shard paths** before writing output. Output flushes create new `Table_N` files; an incremental directory walk would mistake those for additional inputs and never complete.

## Text Encoding

**UTF-8 is supported** for the text backend. Fixed-width encodings are also supported.

- **Allowed:** UTF-8, ASCII, Latin-1 (ISO-8859-1), UTF-16, UTF-32, other fixed-byte-per-char encodings
- **UTF-8 trade-offs:** Variable 1–4 bytes per character; byte-offset seekability is harder. Line-delimited row format still enables line-by-line streaming. For international text and modern compatibility, UTF-8 is preferred.
- **Fixed-width trade-offs:** Each character = fixed bytes; enables byte-offset seekability. Use for legacy or when seekability is critical.

Parameter at database creation: `TextEncoding` or `TextFormat`.

## Error Handling: User-Edited Files

Users may open and edit files manually, corrupting them (wrong length, extra bytes, invalid format).

### Requirements

1. **File name** — Always include in error message
2. **Row number** — Where the error occurred (or closest)
3. **Character position** — Column/byte offset (or closest)
4. **Explanation** — How user interaction might have caused the issue
5. **Actionable** — Enable easy inspection and repair

### Example

```
StorageException: Invalid row length at Tables/Users/Users.txt, row 5, character 42.
Expected 62 bytes per row (schema); found 67. Manual editing may have added or removed characters.
```

## Testing Requirements

- **Test-first** paradigm (TDD)
- **Full unit test coverage** per functional implementation
- Test: high values, low values, multiple inputs, unexpected data
- Test **exception paths** explicitly
- Target: very durable application
