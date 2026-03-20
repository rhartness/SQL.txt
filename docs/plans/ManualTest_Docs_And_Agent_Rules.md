# Manual tests — documentation and agent rules (Sub-plan 2)

## Purpose

Align **human documentation** and **Cursor/AGENT rules** so contributors and coding agents know **how to build, run, interpret manual tests**, and **what artifacts to attach** when reporting failures or performance gaps. This sub-plan defines the **doc and rule updates** that follow the trace/bundle work in [ManualTest_Structured_Logging_And_Diagnostics.md](ManualTest_Structured_Logging_And_Diagnostics.md) and the harness outputs in [ManualTest_Harness_Reporting_Integration.md](ManualTest_Harness_Reporting_Integration.md).

## Goals

- Single **“when manual tests fail”** workflow in [AGENTS.md](../../AGENTS.md) and a **focused Cursor rule** (optional new `.mdc` or extend existing).
- **README / ManualTests README / cli-reference** list all relevant flags (`--storage`, `--require-beat-localdb`, future `--diagnostics`, artifact paths).
- **Documentation standards**: subsection on **diagnostic bundles** for bug reports (what to paste into GitHub/Cursor).
- **Bug report template** (markdown) under `docs/` or `.github/` as appropriate.

## Non-goals

- Rewriting the full Phase 4 spec or storage format docs.
- Mandating OpenTelemetry or external log aggregation.

## Artifacts matrix (reference names)

Align with Sub-plans 1 & 3; update this table when filenames are finalized.

| Artifact | Typical path | Purpose |
|----------|--------------|---------|
| Primary log | `manual-test-artifacts/logs/ManualTests_<ts>.log` | Human timestamped trace |
| Errors + vs LocalDB | `.../ManualTests_<ts>.errors-and-comparison.md` | `#failures`, `#vs-localdb`, `#slower-than-localdb` |
| Diagnostics (planned) | `.../ManualTests_<ts>.diagnostics.jsonl` | Stage/step/counter events for agents |
| Deficits (planned) | `.../ManualTests_<ts>.deficits.md` or section in companion MD | Performance narrative vs comparators |
| Failure bundle (planned) | Embedded in jsonl or `.../ManualTests_<ts>.failure-bundle.json` | Single-file paste for Cursor |

## Agent workflow (“when manual tests fail”)

1. **Reproduce** with documented defaults: `dotnet run --project src/SqlTxt.ManualTests -- <test> [--storage all] [--save-db]` (see [ManualTests README](../../src/SqlTxt.ManualTests/README.md)).
2. **Collect** from the same run timestamp:
   - `ManualTests_<ts>.log`
   - `ManualTests_<ts>.errors-and-comparison.md` — jump to `#failures` or `#slower-than-localdb`.
   - When implemented: `*.diagnostics.jsonl` and `failure-bundle.json` (if generated).
3. **Map** test id to code:
   - Legacy: [ManualTestProgram.cs](../../src/SqlTxt.ManualTests/ManualTestProgram.cs) → `Tests/` or `Compare/`.
   - Phase 4: `phase4-*` → `Phase4*ManualTest.cs` / `LocalDbComparisons.Phase4.cs`.
4. **Sharding / storage issues** — inspect `Tables/<Table>/`, shard files, and bundle `artifactHints` (after Sub-plan 1).
5. **Performance** — use `#vs-localdb` and deficits doc; do not tune without comparable `--rows` / `--storage`.

## Documentation touch list

| Document | Updates |
|----------|---------|
| [AGENTS.md](../../AGENTS.md) | Expand Manual tests subsection: artifact list, failure workflow, link to three sub-plans |
| [.cursor/rules/sql-txt-project-overview.mdc](../../.cursor/rules/sql-txt-project-overview.mdc) | One bullet: observability sub-plans + “attach diagnostics when reporting manual test failures” |
| Optional: `.cursor/rules/sql-txt-manual-tests.mdc` | Short rule: run commands, artifacts, `#` anchors |
| [src/SqlTxt.ManualTests/README.md](../../src/SqlTxt.ManualTests/README.md) | Flags table, artifact paths, link to `docs/plans/ManualTest_*.md` |
| [docs/cli-reference.md](../../docs/cli-reference.md) | If CLI exposes diagnostic flags later, document here |
| [docs/architecture/05-documentation-standards.md](../../docs/architecture/05-documentation-standards.md) | “Diagnostic bundles and manual test attachments” subsection |
| **Template:** [manual-test-bug-report.md](../templates/manual-test-bug-report.md) | Copy-paste template: command line, environment, attachments |

## Bug report template

Use [manual-test-bug-report.md](../templates/manual-test-bug-report.md) as the canonical copy-paste template.

## Rollout order

1. Land Sub-plan 1 + 3 **artifact names** in a small “Observability” note in ManualTests README.
2. Update AGENTS.md + 05-documentation-standards.md + template file.
3. Add or extend Cursor rule(s).

## Definition of done (checklist)

- [ ] AGENTS.md manual-test section references the three `ManualTest_*.md` plans and artifact list.
- [ ] ManualTests README links to sub-plans and documents `#failures` / `#vs-localdb` / `#slower-than-localdb`.
- [ ] 05-documentation-standards.md includes diagnostic bundle guidance.
- [ ] Bug report template exists under `docs/templates/` (or ISSUE_TEMPLATE) and is linked from ManualTests README.
- [ ] Project overview (or new) Cursor rule mentions observability artifacts.

## References

- [ManualTest_Structured_Logging_And_Diagnostics.md](ManualTest_Structured_Logging_And_Diagnostics.md)
- [ManualTest_Harness_Reporting_Integration.md](ManualTest_Harness_Reporting_Integration.md)
