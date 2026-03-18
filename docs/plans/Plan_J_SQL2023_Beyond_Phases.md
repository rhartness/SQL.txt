# Plan J: SQL:2023 Beyond Phases — Roadmap Completion

**Status:** Pending  
**Parent:** Enterprise SQL:2023 Meta-Plan  
**Prerequisites:** Plan I complete

**Reference:** [docs/roadmap/00-sql2023-compliance-roadmap.md](../roadmap/00-sql2023-compliance-roadmap.md)

---

## Scope

- Update roadmap document with final status
- Document deferred SQL:2023 features with phase assignments or "future" designation
- README: Add "SQL:2023 compliance" section with link to roadmap
- Documentation only; minimal or no code changes

---

## Wave 1: Roadmap Document Update

### 1.1 Update 00-sql2023-compliance-roadmap.md

- Mark all phased features as Done
- Update "Deferred" section with final phase assignments for any features moved into phases
- Add "Future Phases" or "Post-Phase 7" section for JSON, arrays, triggers, BLOB/CLOB, etc.
- Reference implementation-defined behavior (ID106, IA201) if documented

### 1.2 Compliance Summary

- Add summary table: Phases 1–7 + CTE = implemented; JSON, arrays, etc. = roadmap
- Link to ISO/IEC 9075:2023 for authoritative feature list

---

## Wave 2: README Update

### 2.1 SQL:2023 Compliance Section

- Add section: "SQL:2023 Compliance"
- Brief statement: SQL.txt implements SQL:2023 features per phase; see [roadmap](docs/roadmap/00-sql2023-compliance-roadmap.md) for full mapping and deferred features
- Link to roadmap in Documentation section (already added in Plan A)

### 2.2 Roadmap Table Final State

- Ensure Roadmap table reflects all phases Done
- SQL:2023 Beyond row: "Roadmap" with link to deferred features

---

## Wave 3: Cross-References

### 3.1 Update References

- AGENTS.md: Ensure roadmap link is prominent
- 11-sql2023-mapping.md: Add "See 00-sql2023-compliance-roadmap.md for deferred features"
- 00-product-spec.md: Update "Full SQL standard support" in Out of Scope if still accurate, or clarify phased approach

---

## Plan Execution Rules (Apply on Completion)

1. Update README
2. Update roadmap document
3. Update cross-references

---

## Deliverable

Comprehensive roadmap documentation. All phased SQL:2023 features documented as implemented. Deferred features (JSON, arrays, triggers, BLOB/CLOB, etc.) documented with "future" or phase assignment. README SQL:2023 compliance section. No code changes (or minimal doc-only).
