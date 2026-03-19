# Phase 4.1 — Query IR, binding, and expression evaluation

**Parent:** [Phase4_Implementation_Plan.md](Phase4_Implementation_Plan.md)

**Goal:** Replace the flat [SelectCommand](../../src/SqlTxt.Contracts/Commands/SelectCommand.cs) shape with a **bound, structured query IR** that supports JOINs, rich WHERE/HAVING, aggregates, ORDER BY, and subqueries—without allocating unbound strings per row in hot paths.

---

## Functional scope

- **FROM / JOIN tree:** base tables, aliases, join type, ON predicate (equi-predicates extracted for planning).
- **SELECT list:** column references (`alias.col`), literals, parameter placeholders (future), **aggregate calls** (parsed and bound to group context).
- **WHERE / HAVING:** boolean expressions with comparison, AND/OR/NOT, parentheses, NULL semantics (three-valued logic) as required by SQL:2023 subset.
- **ORDER BY:** sort keys bound to output columns or expressions.
- **Subquery placeholders:** correlated and uncorrelated subqueries represented as **Apply** / **Subquery** nodes for lowering in [Phase4_05](Phase4_05_Subqueries_Decorrelation_Plan.md).

---

## Efficiency requirements (non-optional)

1. **Binding phase once:** Parse → **bind** (resolve tables/columns/types) → **plan** → **execute**. No name resolution per row during execution.
2. **Predicate evaluation:** Compile bound predicates to **delegates** or a small **bytecode/stack machine** that operates on `RowData` or a slimmer **column value view** to avoid repeated dictionary lookups where a column index is known.
3. **Allocations:** Prefer **rented buffers** (`ArrayPool<byte>`, reusable `StringBuilder` for formatting only at boundaries). Document any path that must allocate per row and justify or eliminate.
4. **Type system:** Use existing [ColumnType](../../src/SqlTxt.Contracts/) metadata for comparison and aggregate dispatch (avoid `Convert.ChangeType` in inner loops).
5. **Constant folding:** Fold literal expressions at plan time (`WHERE 1=0`, `WHERE col = 5+3`).

---

## Components (suggested layout)

| Layer | Responsibility |
|-------|----------------|
| `SqlTxt.Contracts.Queries` (new) | IR records: `SelectQuery`, `JoinNode`, `ScalarExpr`, `BoolExpr`, `SortKey`, `AggregateExpr` |
| `SqlTxt.Parser` | Build IR; separate **expression parser** (Pratt or RD) if `SqlCommandParser` grows unwieldy |
| `SqlTxt.Engine` | `QueryBinder`, `QueryPlanner` (logical plan), `QueryExecutor` |

---

## Risks and mitigations

| Risk | Mitigation |
|------|------------|
| IR churn breaks every command | Version IR; single `ExecuteSelect` entry that dispatches on plan type |
| Duplicate column names after join | SQL rules: require qualification or deterministic rename in binding errors |
| Expression eval security | No `eval`; only whitelisted functions |

---

## Manual test

- `dotnet run --project src/SqlTxt.ManualTests -- phase4-bind-expr` — compound `WHERE` (`AND`); must **pass** when this plan is done (skips until then).

## Acceptance criteria

- [ ] IR covers Phase 4 syntax in [02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md) §4 plus SQL:2023 Phase 4 registry items assigned to parser/planner.
- [ ] Binding errors are **actionable** (unknown column, ambiguous name).
- [ ] Micro-benchmark or profiling note: predicate eval on N rows does not allocate proportional to N for simple equality (document target).
- [ ] Unit tests: binding, constant fold, NULL comparison edge cases.
