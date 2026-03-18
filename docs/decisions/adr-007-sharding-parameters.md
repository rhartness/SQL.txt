# ADR-007: Sharding Parameters

## Status

Accepted

## Context

Table data files can grow large. Sharding splits data across multiple files. Users need configurable control over when shards are created and how large they can become. A rebalance operation allows redistributing data across shards for optimal balance.

## Decision

### Database-Level Default

- **DefaultMaxShardSize:** 20 MB (20,971,520 bytes) as database default
- Stored in `manifest.json` as `defaultMaxShardSize`
- Overridable per table via `CREATE TABLE ... WITH (maxShardSize=...)`

### Split Strategy

When a shard exceeds `MaxShardSize`:

1. Create a new shard file
2. Move approximately half the rows (or tail rows) to the new shard via stream-in/stream-out
3. **Do not** rewrite the entire table
4. Update Shard Table of Contents (STOC) and indexes for affected rows only

### Rebalance API

A `RebalanceTableAsync(tableName)` operation will:

- Scan all shards for the table
- Redistribute rows to balance shard sizes
- Update indexes and STOC
- Be exposed via Engine, Service (`POST /rebalance/{tableName}`), and CLI (`sqltxt rebalance --db ./Db --table Users`)

When Phase 7 (Statistics) is implemented, statistics may inform smarter rebalance decisions (e.g., hot vs. cold shards).

### CREATE DATABASE Syntax

```sql
CREATE DATABASE DemoDb WITH (defaultMaxShardSize=20971520);
```

### CREATE TABLE Syntax

```sql
CREATE TABLE Users (...) WITH (maxShardSize=10485760);  -- 10MB override
```

## Consequences

- Manifest schema extended; existing databases get default on first open or migration
- Parser must support WITH clause for CREATE DATABASE and CREATE TABLE
- Rebalance requires exclusive write lock on table
