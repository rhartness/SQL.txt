# Phase 4.2 — JOIN execution (physical operators)

**Parent:** [Phase4_Implementation_Plan.md](Phase4_Implementation_Plan.md)

**Goal:** Implement **multiple join algorithms** and **plan selection** so equi-joins scale from small OLTP probes to large analytical builds—not a single nested-loop implementation.

---

## Functional scope (SQL surface)

- **F401:** `INNER JOIN`, `LEFT [OUTER] JOIN` with `ON` equality (single- and multi-column equi-join).
- **F407:** `CROSS JOIN` (Cartesian product)—**explicit syntax only**; planner may warn or require row-count cap in config for safety.
- **F406:** `FULL OUTER JOIN`—implement via **union of left-outer + right-anti** or equivalent correct decomposition.
- **F405:** `NATURAL JOIN`—parser desugars to explicit `ON` equality list on common column names.
- **Multi-way:** `FROM A a JOIN B b ON ... JOIN C c ON ...`—plan as **binary join tree** (left-deep or bushy where cost model suggests).

---

## Physical operators (required set)

| Operator | When used | Resource model |
|----------|-----------|----------------|
| **Index Nested Loop (INL)** | Inner equi-key matches PK or secondary index (Phase 3.5 sorted index) | Probe per outer row; **batch probes** (sort outer keys, merge against index) optional optimization |
| **Hash join** | Medium/large inner, no useful index, equi-join | **In-memory hash table** within work memory budget; **spill** partitions to temp files when budget exceeded (grace hash join or hybrid) |
| **Merge join** | Both inputs ordered on join keys (index-order scan or prior sort node) | Streaming merge; **O(1)** extra memory beyond iterators |

**Planner (v1):** Heuristic rules + optional **cost estimates** (row counts from metadata; distinct counts from indexes when available). Interface for **Phase 7 statistics** must be wired so estimates improve without rewriting operators.

---

## I/O and MVCC

- Outer and inner **row sources** must use existing **streaming** table/index APIs ([ITableDataStore](../../src/SqlTxt.Contracts/ITableDataStore.cs), [IIndexStore](../../src/SqlTxt.Contracts/IIndexStore.cs)) with **snapshot** / **NOLOCK** semantics preserved.
- Hash build side may **consume async stream** into partitions; avoid loading entire table if spill is active (partition on hash, write runs).

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Cartesian explosion on CROSS | Configurable **max row product** or explicit opt-in |
| Duplicate matches from non-key inner | Correct SQL semantics (duplicate rows propagate) |
| Shard boundaries | Join reads are **per-table shard iterators**; hash/merge must not assume single file |

---

## Manual test

- `dotnet run --project src/SqlTxt.ManualTests -- phase4-joins` — INNER and LEFT equi-join; must **pass** when join execution meets this plan (skips until then).

## Acceptance criteria

- [ ] At least **three** join implementations active in codebase: INL, hash, merge (merge may be gated on ordered inputs).
- [ ] Planner chooses operator by **documented rules** (even if v1 heuristic); unit tests for choice matrix on synthetic metadata.
- [ ] Spill path tested: hash join with **small work memory** forces partition spill and still **correct**.
- [ ] Integration tests: INNER, LEFT, FULL, CROSS, NATURAL (where in scope), multi-way chain.
- [ ] No full inner table `ReadAllText` in default INL path when index exists.
