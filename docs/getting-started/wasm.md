# Getting Started — WASM Mode

Use the SQL.txt CLI with WASM-compatible storage. The database is stored in a single `.wasmdb` file, simulating the storage backend used in WebAssembly/browser environments.

## Prerequisites

- .NET 8 SDK
- A writable directory for the `.wasmdb` file

## Installation

See [Getting Started (intro)](../getting-started.md) for installation from source or NuGet.

## Quick Start

```bash
# Create a WASM database (persists to ./MyDb.wasmdb)
dotnet run --project src/SqlTxt.Cli -- create-db ./MyDb --wasm

# Create a table
dotnet run --project src/SqlTxt.Cli -- exec --db ./MyDb.wasmdb --wasm "CREATE TABLE User (Id CHAR(10), Name CHAR(50))"

# Insert a row
dotnet run --project src/SqlTxt.Cli -- exec --db ./MyDb.wasmdb --wasm "INSERT INTO User (Id, Name) VALUES ('1', 'Alice')"

# Query
dotnet run --project src/SqlTxt.Cli -- query --db ./MyDb.wasmdb --wasm "SELECT * FROM User"
```

## Build the Sample Wiki in WASM Mode

```bash
dotnet run --project src/SqlTxt.Cli -- build-sample-wiki --db . --wasm
dotnet run --project src/SqlTxt.Cli -- query --db ./WikiDb.wasmdb --wasm "SELECT * FROM User"
```

## Path Semantics

- **create-db ./MyDb --wasm** — Creates `./MyDb.wasmdb`; virtual root is "MyDb"
- **Other commands** — Use `--db ./MyDb.wasmdb --wasm`; path must point to the `.wasmdb` file

## Next Steps

- [WASM Storage Architecture](../architecture/09-wasm-storage.md) — How it works, future browser deployment
- [CLI Reference](../cli-reference.md) — All commands and `--wasm` option
- [Getting Started (CLI)](cli.md) — Filesystem storage
- [Getting Started (Embedding)](embedding.md) — Embed in C# applications
