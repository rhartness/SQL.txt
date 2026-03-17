# SQL.txt — Getting Started

A lightweight, embeddable .NET database engine that persists schemas, metadata, and row data in **human-readable text files**.

## Prerequisites

- .NET 8 SDK
- A writable directory for database storage

## Installation

### From source

```bash
git clone https://github.com/sqltxt/SQLTxt.git
cd SQLTxt
dotnet build
```

### NuGet package (when published)

```xml
<PackageReference Include="SqlTxt" Version="0.1.0" />
```

## Choose Your Path

| Path | Description |
|------|-------------|
| [**CLI (Filesystem)**](getting-started/cli.md) | Use the command-line tool with directory-based storage |
| [**WASM**](getting-started/wasm.md) | Use the CLI with `--wasm` for browser-style single-file storage (`.wasmdb`) |
| [**Embedding (C#)**](getting-started/embedding.md) | Embed the engine in your C# application |

## Next Steps

- [CLI Reference](cli-reference.md) — All commands and options
- [Sample Wiki Database](samples/wiki-database.md) — Example schema and scripts
- [Architecture](architecture/01-system-architecture.md) — System design
- [WASM Storage](architecture/09-wasm-storage.md) — Browser-compatible storage design
