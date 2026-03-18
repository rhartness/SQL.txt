# Plan A: Foundation — Documentation and Roadmap

**Status:** Done  
**Parent:** Enterprise SQL:2023 Meta-Plan

## Scope

- Create [docs/roadmap/00-sql2023-compliance-roadmap.md](../roadmap/00-sql2023-compliance-roadmap.md) — References ISO/IEC 9075:2023; lists all SQL:2023 features not yet in phases; maps each to a future phase or "deferred"
- Add Plan Execution Rules to [AGENTS.md](../../AGENTS.md) and [docs/architecture/05-documentation-standards.md](../architecture/05-documentation-standards.md)
- Update [README.md](../../README.md): Add "Storage backends (text | binary)" to Features; update Roadmap with new goals; add link to roadmap doc
- Update [docs/specifications/00-product-spec.md](../specifications/00-product-spec.md): Remove "Human-readable formats only"; add "Dual storage backends (text, binary)"
- Update [.cursor/rules/sql-txt-project-overview.mdc](../../.cursor/rules/sql-txt-project-overview.mdc): Add storageBackend to manifest; reference dual storage

## Deliverable

Documentation foundation; no code changes.

## Acceptance

- [x] docs/roadmap/00-sql2023-compliance-roadmap.md exists
- [x] Plan Execution Rules in AGENTS.md and 05-documentation-standards.md
- [x] README updated with storage backends, roadmap link, phase table
- [x] 00-product-spec.md updated
- [x] sql-txt-project-overview.mdc updated
