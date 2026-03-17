# Getting Started — CLI (Filesystem)

Use the SQL.txt CLI with filesystem storage. Databases are stored as directories of human-readable text files.

## Prerequisites

- .NET 8 SDK
- A writable directory for database storage

## Installation

See [Getting Started (intro)](../getting-started.md) for installation from source or NuGet.

## Quick Start

```bash
# Create a database
dotnet run --project src/SqlTxt.Cli -- create-db ./MyDb

# Create a table
dotnet run --project src/SqlTxt.Cli -- exec --db ./MyDb "CREATE TABLE User (Id CHAR(10), Name CHAR(50))"

# Insert a row
dotnet run --project src/SqlTxt.Cli -- exec --db ./MyDb "INSERT INTO User (Id, Name) VALUES ('1', 'Alice')"

# Query
dotnet run --project src/SqlTxt.Cli -- query --db ./MyDb "SELECT * FROM User"
```

## Build the Sample Wiki

```bash
dotnet run --project src/SqlTxt.Cli -- build-sample-wiki --db .
dotnet run --project src/SqlTxt.Cli -- query --db ./WikiDb "SELECT * FROM User"
```

## Next Steps

- [CLI Reference](../cli-reference.md) — All commands and options
- [Sample Wiki Database](../samples/wiki-database.md) — Schema and scripts
- [Getting Started (WASM)](wasm.md) — Use `--wasm` for browser-style storage
- [Getting Started (Embedding)](embedding.md) — Embed in C# applications
