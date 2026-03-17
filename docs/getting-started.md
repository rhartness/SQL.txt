# Getting Started with SQL.txt

## Prerequisites

- **.NET 8 SDK** (or compatible; `rollForward` allows newer)
- **Writable directory** for database storage
- **Windows, macOS, or Linux** — SQL.txt is cross-platform

## Build

```bash
git clone <repository-url>
cd SQLTxt
dotnet build
```

## Run Tests

```bash
dotnet test
```

## Minimal Example

Once the engine is implemented, you can:

1. **Create a database**

```bash
sqltxt create-db ./MyDatabase
```

Or via API:

```csharp
var engine = SqlTxtDatabase.Open("./MyDatabase");
engine.Execute("CREATE DATABASE MyDatabase;");
```

2. **Create a table**

```sql
CREATE TABLE Users (
    Id CHAR(10),
    Name CHAR(50),
    Email CHAR(100)
);
```

3. **Insert and query**

```sql
INSERT INTO Users (Id, Name, Email) VALUES ('1', 'Alice', 'alice@example.com');
SELECT * FROM Users;
```

## Next Steps

- [Architecture](architecture/01-system-architecture.md) — System design
- [Storage Format](architecture/02-storage-format.md) — On-disk layout (db/, Tables/, ~System/)
- [CLI Reference](cli-reference.md) — Command-line usage
- [Sample Wiki Database](samples/wiki-database.md) — Example schema, scripts, and CLI calls
