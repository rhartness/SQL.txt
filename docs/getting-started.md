# SQL.txt — Getting Started

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

## Quick Start

### CLI

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

### WASM mode (browser-compatible storage)

Use `--wasm` to store the database in a single `.wasmdb` file. This simulates the storage backend used in WebAssembly/browser environments.

```bash
dotnet run --project src/SqlTxt.Cli -- create-db ./MyDb --wasm
dotnet run --project src/SqlTxt.Cli -- exec --db ./MyDb.wasmdb --wasm "CREATE TABLE User (Id CHAR(10), Name CHAR(50))"
dotnet run --project src/SqlTxt.Cli -- exec --db ./MyDb.wasmdb --wasm "INSERT INTO User (Id, Name) VALUES ('1', 'Alice')"
dotnet run --project src/SqlTxt.Cli -- query --db ./MyDb.wasmdb --wasm "SELECT * FROM User"
```

### Embedding (C#)

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

## Next Steps

- [CLI Reference](cli-reference.md)
- [Sample Wiki Database](samples/wiki-database.md)
- [Architecture](architecture/01-system-architecture.md)
