# Phase 4 — Query enrichment (master plan)

**Status:** Planned (implementation not started)

**Purpose:** Deliver **Phase 4** query features (JOINs, aggregates, ORDER BY, GROUP BY/HAVING, subqueries, SQL:2023 items F401/F405–F407, T626, F868) with **enterprise-grade throughput and resource discipline**. This plan sets **non-negotiable efficiency and robustness bars**; feature-specific work is broken into sub-plans so each area can be reviewed, implemented, and tested without “easy path” shortcuts that break at scale.

**Specifications:** [02_Post_Phase3_Features.md](../specifications/02_Post_Phase3_Features.md) §4  
**SQL:2023:** [01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md) (Phase 4)  
**Efficiency doctrine:** [10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md), [Efficiency_Audit_Methodology.md](Efficiency_Audit_Methodology.md)

---

## Product positioning (performance)

SQL.txt targets **high-throughput, single-node embedded** workloads: local applications, services, and tools where latency and CPU/memory efficiency matter. For Phase 4, **correctness is mandatory**; **algorithmic and I/O behavior must be chosen for scalability**, not demo-quality prototypes.

**Comparative baseline (intent):** On comparable hardware and **local, single-node** scenarios, operators should be **competitive with** general-purpose engines (e.g. SQL Server LocalDB, MySQL/InnoDB, SQLite-class) for:

- **Join throughput** when cardinalities and selectivity vary (not only tiny nested loops on indexed equality).
- **Sort and top-N** behavior under memory pressure (bounded RAM, spill when needed).
- **Aggregation** over large inputs without gratuitous full materialization where streaming or hashing is standard practice.

**Reality check:** SQL.txt remains **file-backed** with a distinct storage model. “As efficient as SQL Server” does **not** mean identical wall-clock on every query; it means **no known O(n²) or unbounded-memory patterns** where industry practice uses hash/sort/spill, **prefer streaming I/O** for scans, and **select join and sort algorithms by cost/model**, not by whichever was quickest to code.

---

## What “no easy path” means (reject list)

The following are **explicitly rejected** as final designs for Phase 4 (they may appear only as **temporary scaffolding** behind a feature flag during development, and must be removed before marking the feature Done):

| Anti-pattern | Why rejected | Required direction |
|--------------|--------------|-------------------|
| Always materialize full join output then sort in RAM | Unbounded memory | Streaming pipelines, **top-N**, **index-order** scans, **external sort** when needed ([Phase4_03](Phase4_03_OrderBy_Sort_Plan.md)) |
| Only nested-loop join for all equi-joins | Degrades on large inner without index | **Hash join** for medium/large build sides; **index nested loop** when inner key is indexed; **merge join** when inputs ordered on join keys ([Phase4_02](Phase4_02_Joins_Execution_Plan.md)) |
| Correlated subquery = repeated full re-execution only | Classic O(outer × subquery full scan) trap | **Decorrelation**, **semi-join / anti-join** lowering, **result caching** where safe ([Phase4_05](Phase4_05_Subqueries_Decorrelation_Plan.md)) |
| Monolithic string parsing in hot path | Allocations, no vectorization | **Structured IR**, bound pools, **Pratt/recursive descent** with clear eval layer ([Phase4_01](Phase4_01_Query_IR_and_Expressions_Plan.md)) |
| GROUP BY = sort entire row width | Memory bloat | **Hash aggregate** on group key; **sort-based aggregate** only when beneficial; **spill** for large groups ([Phase4_04](Phase4_04_GroupBy_Aggregates_Plan.md)) |

---

## Cross-cutting requirements (all Phase 4 features)

1. **Memory governance:** Configurable **work memory budget** per query (or per operator). When exceeded, operators **spill** to temp storage (aligned with durability rules: temp file + atomic commit semantics where applicable). No silent unbounded `List<Row>` growth for production paths without documented opt-in.
2. **I/O:** Prefer **true streaming** (`IAsyncEnumerable`, shard-aware readers) through join/build sides where the plan allows; avoid `ReadAllTextAsync` for large tables on hot paths ([10-performance-and-efficiency.md](../architecture/10-performance-and-efficiency.md)).
3. **Indexes:** Exploit Phase 3.5 **sorted index files** and PK for **index seeks**, **merge join**, and **order-preserving** plans ([adr-008](../decisions/adr-008-index-shard-structure.md)).
4. **MVCC / NOLOCK:** Any new operator must thread **snapshot** and **locking hints** consistently with existing SELECT ([08-concurrency-and-locking.md](../architecture/08-concurrency-and-locking.md)).
5. **Observability:** Plan shape (at least logical operator names + cardinalities if available) suitable for future **EXPLAIN**; log hooks or metrics points for join/sort/spill (even if UI comes later).
6. **Statistics (Phase 7 hook):** Phase 4 **cost-based choices** should call a small **ICardinalityEstimator** / **IStatisticsProvider** abstraction (stub acceptable initially) so Phase 7 statistics plug in without rewriting join code.
7. **Testing:** Each sub-plan defines **correctness** tests plus **scale tests** (synthetic larger inputs) and, where applicable, **benchmarks** in ManualTests or a dedicated perf project.

---

## Risk and efficiency (expanded — all items addressed)

### Memory risk

- **Risk:** JOIN + ORDER BY + GROUP BY combine to multiply resident sets.
- **Mitigation:** Operator-specific budgets; **spill** for hash tables and sort runs; **pipeline** where possible; **early projection** (drop columns as soon as not needed).

### CPU and allocation risk

- **Risk:** Per-row allocations in join and expression evaluation dominate.
- **Mitigation:** Reusable buffers (`ArrayPool`), struct-backed row handles where feasible, compiled expression paths or cached delegates for repeated predicates; document trade-offs in [Phase4_01](Phase4_01_Query_IR_and_Expressions_Plan.md).

### I/O risk

- **Risk:** Repeated full-table reads for subqueries and nested loops.
- **Mitigation:** Index-aware plans; **batch** inner lookups; decorrelation; optional **mid-query cache** of immutable snapshot reads within one SELECT execution.

### Parser / maintainability risk

- **Risk:** One giant `ParseSelect` becomes unmaintainable and slow to compile expressions.
- **Mitigation:** Separate **query IR** and **expression** modules; clear separation parse → bind → plan → execute.

### Planning risk (wrong algorithm for the data)

- **Risk:** Hash join on tiny tables wastes setup; nested loop on millions of rows wastes time.
- **Mitigation:** **Cost model** (even heuristic v1: row estimates from table metadata + index distinct counts, upgraded in Phase 7); **runtime adaptive** hooks optional later.

### Correctness under edge cases

- **Risk:** NULL join semantics, duplicate join keys, floating DECIMAL, empty inputs.
- **Mitigation:** SQL:2023-aligned tests; property-style cases listed in each sub-plan.

---

## Sub-plans (feature-specific)

| Sub-plan | Scope |
|----------|--------|
| [Phase4_01_Query_IR_and_Expressions_Plan.md](Phase4_01_Query_IR_and_Expressions_Plan.md) | AST/IR, binding, evaluation, constants, allocation strategy |
| [Phase4_02_Joins_Execution_Plan.md](Phase4_02_Joins_Execution_Plan.md) | INL, hash, merge join; multi-way joins; CROSS/FULL/NATURAL |
| [Phase4_03_OrderBy_Sort_Plan.md](Phase4_03_OrderBy_Sort_Plan.md) | In-memory sort bounds, top-N, index order, external sort |
| [Phase4_04_GroupBy_Aggregates_Plan.md](Phase4_04_GroupBy_Aggregates_Plan.md) | Hash/sort aggregates, HAVING, ANY_VALUE (T626), F868 |
| [Phase4_05_Subqueries_Decorrelation_Plan.md](Phase4_05_Subqueries_Decorrelation_Plan.md) | IN, EXISTS, scalar; decorrelation; semi/anti-join |

Implement in **dependency order:** **01 → 02 → 03 → 04 → 05**, with thin vertical slices (e.g. IR + simple join) early to validate plumbing.

---

## Manual tests (SqlTxt.ManualTests)

Each Phase 4 sub-plan has a **named manual test** (run from repo root). Until the feature is implemented, the test **SKIPs** (`ParseException` → `Details["Skipped"]=true`, exit code 0). After implementation, the same test must **PASS** with real assertions.

| Sub-plan | Manual test command |
|----------|---------------------|
| 4.1 IR / expressions | `dotnet run --project src/SqlTxt.ManualTests -- phase4-bind-expr` |
| 4.2 Joins | `dotnet run --project src/SqlTxt.ManualTests -- phase4-joins` |
| 4.3 ORDER BY / sort | `dotnet run --project src/SqlTxt.ManualTests -- phase4-orderby` |
| 4.4 GROUP BY / aggregates | `dotnet run --project src/SqlTxt.ManualTests -- phase4-groupby` |
| 4.5 Subqueries | `dotnet run --project src/SqlTxt.ManualTests -- phase4-subqueries` |
| All Phase 4 | `dotnet run --project src/SqlTxt.ManualTests -- phase4-all` |

Use `--storage all` to run each Phase 4 test on text and binary backends. **`--compare:localdb` is ignored** for all `phase4-*` tests (LocalDB parity is not defined for these scenarios and LocalDB may not expose the same feature surface under test).

---

## Milestone acceptance (master)

- [ ] All sub-plans marked **Done** with their local acceptance criteria met.
- [ ] No rejected anti-patterns remain in default code paths.
- [ ] `dotnet test` green; manual tests extended if locking/sharding/scan behavior changes; **Phase 4 manual tests above pass** (not skipped) when Phase 4 ships.
- [ ] [11-sql2023-mapping.md](../architecture/11-sql2023-mapping.md) and [01-sql2023-feature-registry.md](../roadmap/01-sql2023-feature-registry.md) updated for implemented Phase 4 IDs.
- [ ] [cli-reference.md](../cli-reference.md) and embedding docs updated with Phase 4 examples.

---

## Out of scope (Phase 4)

- **CTE / WITH** — separate [Phase_CTE_Plan.md](Phase_CTE_Plan.md).
- **Distributed / parallel query** — not required for Phase 4; design hooks (partitioning of work) may be documented for future phases.
- **Full cost-based optimizer** — heuristic v1 required; deep CBO deferred until statistics (Phase 7) unless sub-plan specifies a minimal model.
