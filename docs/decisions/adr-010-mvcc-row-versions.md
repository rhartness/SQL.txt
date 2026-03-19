# ADR-010: MVCC row versions (`xmin` / `xmax`) and snapshot reads

## Status

Accepted (pre-release; format may change without migration tooling — see **Pre-release policy**).

## Context

The product goal includes **true MVCC**: readers observe a **snapshot** of committed row versions while writers append new versions. The storage engine uses **append-oriented** shards (text and binary), which fits **append new version + mark old version invalid** rather than in-place overwrites for updates.

## Decision

### Row version metadata

Each **physical** row line (text) or record (binary) carries:

- **`xmin`** — 64-bit transaction id that **created** this version (padded decimal in text; binary little-endian).
- **`xmax`** — 64-bit transaction id that **invalidated** this version; **`0`** means “still valid / not superseded”.

Legacy rows **without** trailing MVCC fields are interpreted as **`xmin = 1`**, **`xmax = 0`** for development databases only (pre-release policy).

### Transaction ids (`xid`)

- Monotonic **64-bit** counter per database, persisted under `~System` (same durability pattern as row-id sequences).
- **Auto-commit:** each INSERT/UPDATE/DELETE statement allocates one or two xids as needed: **same xid** may mark delete of old version and insert of new version for UPDATE.

### Visibility (snapshot at read)

At **`SELECT`** (without `NOLOCK`), capture **`snapshotXid = committedXid`** read atomically at statement start (`Volatile.Read` / persisted watermark).

A row version **V** is **visible** when:

- `V.xmin <= snapshotXid`
- `V.xmax == 0` **or** `V.xmax > snapshotXid`

`NOLOCK` **does not** apply this filter for isolation guarantees; implementation may still parse rows best-effort for reporting.

### INSERT / UPDATE / DELETE

- **INSERT:** append row with `xmin = xid`, `xmax = 0`.
- **UPDATE:** set prior latest version’s `xmax = xid`; append new row with `xmin = xid`, `xmax = 0`.
- **DELETE:** set latest version’s `xmax = xid` (soft MVCC delete; line may remain until vacuum).

### Indexes

Indexes reference **logical row identity** (`_RowId` / PK). Probes return candidate row ids; visibility is applied on **fetch** using MVCC rules so obsolete index entries are harmless if filtered.

### Vacuum

Periodic or on-demand **shard rewrite** drops physical versions where `xmax != 0` and `xmax` is **below** a **safe watermark** (minimum snapshot still needed — for auto-commit single-node, `committedXid` minus conservative slack or “all readers finished” if extended later). Integrated with existing transform/rebalance paths.

### Pre-release policy

The project is **unpublished**: storage layout may advance **without** upgrade tooling. Breaking MVCC layout changes are acceptable; sample databases can be recreated.

## Consequences

- **Positive:** Readers and writers on **different keys** need not block each other for the full duration of table rewrites once table-lock scopes shrink.
- **Positive:** Undo path for future multi-statement transactions can reuse version chains.
- **Negative:** Index and FK validation must remain correct when multiple versions share a key — **latest visible** version wins for uniqueness checks.

## Alternatives considered

- **No MVCC; table locks only** — Rejected per product roadmap.
- **Delta files per transaction** — More indirection; deferred in favor of inline `xmin`/`xmax`.
