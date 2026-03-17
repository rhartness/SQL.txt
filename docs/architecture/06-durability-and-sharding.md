# Durability, Sharding, and Error Handling

## Core Principle

Use **absolute best practices** for breaking up parts that might grow very large. As data is added to any data file (table data, indexes, multi-width field files), these files must be **shardable**.

## Sharding

### Per-Table Parameter: MaxShardSize

Each table has a configurable `MaxShardSize` (bytes or rows). When a data file exceeds this limit, the engine creates a new shard file.

### Sharding Strategy

- **Table data files:** Shard when too large. Naming: `<TableName>_1.txt`, `<TableName>_2.txt`, etc., or `<TableName>_shard_001.txt`
- **Indexes (Phase 2+):** Do **not** shard initially. Indexes reference shard file + row/offset.
- **Metadata:** Keep metadata files small; shard if they grow (e.g., `~System` table data)

### Future Indexes

When indexes are added, they must know **file references** (which shard, which row). Index entries point to shard + position.

## Text Encoding

**Only fixed-width encodings.** Each character = fixed number of bytes in the file.

- **Allowed:** ASCII, Latin-1 (ISO-8859-1), UTF-16, UTF-32, other fixed-byte-per-char encodings
- **Not allowed:** UTF-8 (variable 1–4 bytes per character)

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
