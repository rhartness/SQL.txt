# Phase 4.3 — ORDER BY, sort, and top-N

**Parent:** [Phase4_Implementation_Plan.md](Phase4_Implementation_Plan.md)

**Goal:** Sorting and ordering that **scale with bounded memory**: avoid “materialize everything then `List.Sort`” as the only production path.

---

## Functional scope

- `ORDER BY expr [ASC|DESC] [, ...]` with **stable sort** semantics (tie-breakers documented: e.g. row id or input order).
- **Qualified** sort keys in JOIN queries.
- **F868 (ORDER BY in grouped query):** Per SQL:2023 subset chosen for SQL.txt, document and implement interaction with `GROUP BY` (may require `ANY_VALUE` or functional dependency rules—align with [Phase4_04](Phase4_04_GroupBy_Aggregates_Plan.md)).

---

## Physical strategies (required, not pick-one)

| Strategy | When | Behavior |
|----------|------|----------|
| **Index order scan** | `ORDER BY` matches **leftmost** indexed key and **no conflicting** prior operators | Avoid sort node; **order-preserving** scan from index or PK |
| **In-memory sort** | Result cardinality within **sort budget** (row count × key width estimate) | Timsort or introspective sort on **key array + row handles** (not full row copies if avoidable) |
| **External sort** | Exceeds budget | **K-way merge** of sorted runs spilled to temp storage; merge phase streams to consumer |
| **Top-N / heap** | `FETCH FIRST` / `TOP` (if added) or optimizer detects **limit** | **Binary heap** of size N; avoid full sort |

If the engine does not expose `TOP`/`LIMIT` in Phase 4, still implement **heap optimization** behind an internal API or future keyword so ORDER BY+limit is not O(n log n) full sort when limit is added.

---

## Efficiency requirements

1. **Key extraction:** Sort **keys** (and pointers/handles to rows), not duplicate full wide rows unless necessary for projection order.
2. **Comparisons:** Use **typed comparers** (string ordinal vs culture documented; numeric DECIMAL rules consistent with Core).
3. **Spill files:** Same durability discipline as storage: temp runs, atomic visibility rules documented in [02-storage-format.md](../architecture/02-storage-format.md) or a dedicated “query temp” section.
4. **Streaming output:** After sort, results can stream to client; external merge should **not** require second full materialization in RAM.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| VARCHAR long keys blow memory | Key prefix sort + tie-breaker row id, or length-aware spill run format |
| ORDER BY after JOIN multiplies rows | Sort after join only when required; push sort under join when semantically valid (advanced; document) |

---

## Manual test

- `dotnet run --project src/SqlTxt.ManualTests -- phase4-orderby` — multi-key `ORDER BY`; must **pass** when sort/index-order behavior meets this plan (skips until then).

## Acceptance criteria

- [ ] **Index-order** path tested: single-table SELECT with ORDER BY on indexed column produces **no sort operator** in logical plan (observable via internal explain or test hook).
- [ ] **External sort** tested: synthetic input larger than memory budget completes with correct order.
- [ ] Stable ordering verified by test with duplicate sort keys.
- [ ] Documented memory budget knob and default value.
