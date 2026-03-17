# ADR-001: Human-Readable Storage Format

## Status

Accepted

## Context

SQL.txt aims to be both a learning tool and a practical embedded datastore. A key differentiator is that users can inspect and debug data with any text editor.

## Decision

All on-disk storage (schema, metadata, row data) uses human-readable text formats:

- Schema: pipe-delimited, line-based (e.g., `1|Id|CHAR|10`)
- Metadata: key-value or simple tabular text
- Row data: fixed-width positional format with soft-delete marker (Phase 1)

No binary formats for core data. Optional binary indexes in Phase 2 may be considered if readability can be preserved (e.g., one-value-per-line index files).

## Consequences

### Positive

- Easy to inspect, debug, and diff
- Teachable: users see exactly what the engine stores
- No special tools required
- Version control friendly
- Corruption more easily detectable

### Negative

- Larger file sizes than binary
- Slower for very large datasets
- Parsing overhead
- Escaping/delimiter considerations for variable-width (Phase 3)

## Alternatives Considered

- **Binary format**: Rejected for core data; reduces learning value and inspectability.
- **JSON for rows**: Considered; rejected for Phase 1 in favor of compact fixed-width; may revisit for VARCHAR.
- **SQLite-style**: Out of scope; we are building a new engine, not wrapping existing ones.
