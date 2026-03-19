# Phase 4.5 — Subqueries: IN, EXISTS, scalar; decorrelation

**Parent:** [Phase4_Implementation_Plan.md](Phase4_Implementation_Plan.md)

**Goal:** Subquery execution using **algebraic lowering** where possible (`IN` → semi-join, `NOT IN` / `NOT EXISTS` → anti-join), **not** only “re-run inner query per outer row” for every shape.

---

## Functional scope

1. `WHERE col IN (SELECT ...)` — **semi-join** when decorrelatable; fallback execution documented.
2. `WHERE EXISTS (SELECT 1 FROM ... WHERE ...)` / `NOT EXISTS` — **semi-join** / **anti-join**.
3. **Scalar subquery** in SELECT list and WHERE: **exactly one row** expected; zero rows → NULL; multiple rows → error per SQL rules.
4. **Correlated** subqueries: **decorrelate** when pattern matches (e.g. equi-correlation on outer column = inner column); retain **indexed nested apply** with **memoization** or **batch** when decorrelation is not possible.

---

## Lowering rules (required direction)

| Pattern | Preferred physical shape |
|---------|---------------------------|
| `EXISTS` correlated equi-join | **Semi-join** (hash or INL) with outer column = inner key |
| `IN (subquery)` decorrelatable | **Distinct** inner build + **semi-join** or **hash semi-join** |
| `NOT EXISTS` | **Anti-join** |
| Scalar agg subquery `SELECT COUNT(*)` | **Precompute** once if uncorrelated; **grouped side** if correlated with group key |

Document **non-lowering** cases and the **Apply** iterator used (nested loop with inner rebind), with **row/block batching** on inner parameters to reduce repeated setup.

---

## Caching and correctness

- **Snapshot consistency:** Inner and outer reads share the same **MVCC snapshot** for one SELECT statement.
- **Cache:** Optional **per-query cache** of inner results keyed by correlation parameters (bounded size, eviction) for repeated parameters in nested loop apply.

---

## Efficiency requirements

1. **Uncorrelated** subqueries: execute **once** and reuse (classic mistake is N executions).
2. **Correlated:** Prefer **decorrelation** to join; if not possible, **batch** inner lookups (collect outer keys, probe index in batch).
3. Avoid building full inner result as `List` when **semi-join** can short-circuit on first match.

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| `NOT IN` + NULL inner | Three-valued logic traps—SQL-standard tests |
| Decorrelation wrong on duplicates | Semi-join semantics vs inner duplicates |
| Unbounded recursion of subqueries | Depth limit or stack guard in planner |

---

## Manual test

- `dotnet run --project src/SqlTxt.ManualTests -- phase4-subqueries` — `IN`, `EXISTS`, scalar subquery; must **pass** when subqueries meet this plan (skips until then).

## Acceptance criteria

- [ ] Uncorrelated IN/EXISTS executes inner **once** (assert via test hook or counter).
- [ ] At least one **decorrelated** correlated EXISTS plan tested (same result as nested execution).
- [ ] Scalar subquery error cases: 0 rows → NULL, >1 row → error.
- [ ] Document remaining patterns that still use Apply/nested loop and roadmap to improve.
