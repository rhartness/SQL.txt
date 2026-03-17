# Getting Started — Embedding (C#)

Embed SQL.txt in your C# application. Use the `DatabaseEngine` directly with filesystem storage.

## Prerequisites

- .NET 8 SDK
- Add SqlTxt package reference (or project reference)

## Installation

### From source

```bash
git clone https://github.com/sqltxt/SQLTxt.git
cd SQLTxt
dotnet build
```

Add a project reference to SqlTxt.Engine (or the SqlTxt package when published).

### NuGet package (when published)

```xml
<PackageReference Include="SqlTxt" Version="0.1.0" />
```

## Quick Start

```csharp
using SqlTxt.Engine;

var engine = new DatabaseEngine();

// Create database (path = parent directory)
await engine.ExecuteAsync("CREATE DATABASE MyDb", ".");

// Create table and insert
await engine.ExecuteAsync("CREATE TABLE User (Id CHAR(10), Name CHAR(50))", "./MyDb");
await engine.ExecuteAsync("INSERT INTO User (Id, Name) VALUES ('1', 'Alice')", "./MyDb");

// Query
var result = await engine.ExecuteQueryAsync("SELECT * FROM User", "./MyDb");
foreach (var row in result.QueryResult!.Rows)
{
    Console.WriteLine($"{row.GetValue("Id")}: {row.GetValue("Name")}");
}
```

## WASM Storage (Programmatic)

To use WASM-compatible storage from code, inject `PersistedMemoryFileSystemAccessor`:

```csharp
using SqlTxt.Engine;
using SqlTxt.Storage;

var persistencePath = Path.GetFullPath("./MyDb.wasmdb");
var fs = new PersistedMemoryFileSystemAccessor(persistencePath);
var engine = new DatabaseEngine(fs: fs);

await engine.ExecuteAsync("CREATE DATABASE MyDb", ".");
await engine.ExecuteAsync("CREATE TABLE User (Id CHAR(10), Name CHAR(50))", "MyDb");
// ... use "MyDb" as database path (virtual root)
```

## Next Steps

- [API and Deployment](../architecture/07-api-and-deployment.md) — Build types and API surface
- [CLI Reference](../cli-reference.md) — CLI usage for comparison
- [Getting Started (CLI)](cli.md) — CLI with filesystem
- [Getting Started (WASM)](wasm.md) — CLI with WASM storage
