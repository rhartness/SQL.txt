# Phase 4.4 — GROUP BY, aggregates, HAVING, ANY_VALUE (T626), F868

**Parent:** [Phase4_Implementation_Plan.md](Phase4_Implementation_Plan.md)

**Goal:** Aggregations that match **industry-standard** behavior: **hash-based grouping** for large inputs, **sort-based** grouping when input is pre-ordered, **spill** for large groups, and correct **NULL** handling per aggregates.

---

## Functional scope

- Aggregates: `COUNT(*)`, `COUNT(expr)`, `SUM`, `AVG`, `MIN`, `MAX` with SQL NULL semantics.
- `GROUP BY` one or more keys (expressions allowed per IR).
- `HAVING` with predicates over aggregates and group keys.
- **T626 `ANY_VALUE(expr)`** for selected columns not in GROUP BY when required by chosen SQL rules.
- **F868:** Document and implement **ORDER BY** interaction with grouped queries (aligned with registry and [02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md)).

---

## Physical operators (required set)

| Operator | When | Notes |
|----------|------|--------|
| **Hash aggregate** | Default for large unknown cardinality | Open-addressing or bucket chain; **spill** partitions when work memory exceeded |
| **Stream / sort aggregate** | Input already ordered on **all** group-by keys | Single pass merge of consecutive equal keys |
| **Scalar aggregate** | No GROUP BY | Single accumulator; still use typed eval |

**AVG:** Use **running sum + count** in integer/decimal-safe types; watch overflow ( widen to `decimal` or checked math).

---

## JOIN + GROUP BY interaction

- Typical plan: **Join → Aggregate**. Push **predicate** before join when possible; **push aggregate** below join only when semantically valid (group-by on join key subset).
- Memory: joining before aggregate can **inflate** row width; **early projection** after join to columns needed for aggregate and GROUP BY.

---

## Efficiency requirements

1. Do not **group on full row hash** unless keys are full row; hash on **normalized group key** representation (typed key struct or pooled key bytes).
2. **DISTINCT** (if introduced with aggregates) shares infrastructure with **hash aggregate** (dedupe phase).
3. **Parallelism:** Not required Phase 4; structure accumulators so **parallel partial aggregate** could be added later (thread-local partials + merge).

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| Huge cardinality groups | Spill + partition; two-phase aggregate |
| DECIMAL precision | Match Core rules; regression tests vs known decimals |
| Wrong results with LEFT JOIN and COUNT(*) | Standard SQL tests (count rows vs count non-null) |

---

## Manual test

- `dotnet run --project src/SqlTxt.ManualTests -- phase4-groupby` — `GROUP BY` / `COUNT` / `SUM` / `HAVING`; must **pass** when aggregates meet this plan (skips until then).

## Acceptance criteria

- [ ] Hash aggregate with **spill** tested (many distinct groups, low memory).
- [ ] Stream aggregate tested on **pre-sorted** input (index order).
- [ ] HAVING filters groups after aggregate; WHERE filters before (integration tests).
- [ ] `ANY_VALUE` and F868 behavior documented and tested.
- [ ] Registry updated for T626/F868 when implemented.
