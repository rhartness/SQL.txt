# Testing Strategy

## Unit Tests

### Parser

- Tokenization of keywords, identifiers, literals
- Statement parsing for each command type
- Error reporting (invalid syntax, line/column)
- Identifier validation

### Storage

- Schema serialization/deserialization
- Metadata read/write
- Row format parsing (fixed-width, soft-delete marker)
- Format version handling

### Core

- Identifier normalization
- Width validation
- Data type validation

### Engine

- Command validation
- Execution flow (mocked storage)

## Integration Tests

- Create database → verify directory and manifest
- Create table → verify schema and metadata files
- Insert rows → verify data file contents
- Select → verify returned rows
- Update → verify modified rows and metadata
- Delete → verify soft-delete marker and counts
- Re-open existing database and query persisted data

## Golden File Tests

Use known input and expected output files:

- Schema files (exact format)
- Data files (row layout)
- Metadata files

These support Cursor-driven implementation and regression detection.

## Parser Test Cases

- Valid statements for each command type
- Invalid syntax (missing semicolon, malformed WHERE, etc.)
- Edge cases: empty values, max width

## Corruption Handling

- Malformed schema detection
- Invalid metadata recovery
- Future: checksum or validation on read

## Test Organization

- `SqlTxt.Core.Tests` — Core unit tests
- `SqlTxt.Storage.Tests` — Storage unit tests
- `SqlTxt.Parser.Tests` — Parser unit tests
- `SqlTxt.Engine.Tests` — Engine unit tests
- `SqlTxt.IntegrationTests` — End-to-end scenarios
