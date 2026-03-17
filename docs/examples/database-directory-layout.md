# Example Database Directory Layout

This document shows the on-disk structure using the specified layout: root folder = database name, `db/` for database properties, `Tables/` for user tables, `~System/` for system metadata.

## Create Database and Table

```sql
CREATE DATABASE DemoDb;
CREATE TABLE Users (
    Id CHAR(10),
    Name CHAR(50),
    Email CHAR(100)
);
INSERT INTO Users (Id, Name, Email) VALUES ('1', 'Richard', 'richard@example.com');
INSERT INTO Users (Id, Name, Email) VALUES ('2', 'Alice', 'alice@example.com');
```

## Resulting Directory Structure

```
DemoDb/
├── db/
│   └── manifest.json
├── Tables/
│   └── Users/
│       ├── Users.txt              # Root data file
│       ├── Users_PK.txt           # Primary key (Phase 2+)
│       └── (schema/metadata)
└── ~System/
    └── (system tables for meta-information)
```

## File Contents

### db/manifest.json

```json
{
  "engineVersion": "0.1.0",
  "storageFormatVersion": 1
}
```

### Tables/Users/Users.txt

```
A|1         Richard                                           richard@example.com
A|2         Alice                                             alice@example.com
```

### Tables/Users/ (schema)

Schema and metadata for the Users table (exact format may live in table folder or ~System per implementation).

## With Indexes and Foreign Keys (Phase 2+)

```
DemoDb/
├── db/
│   └── manifest.json
├── Tables/
│   ├── Users/
│   │   ├── Users.txt
│   │   ├── Users_PK.txt
│   │   └── Users_INX_Email_1.txt
│   └── Orders/
│       ├── Orders.txt
│       ├── Orders_PK.txt
│       └── Orders_FK_Users.txt
├── Views/
├── Procedures/
├── Functions/
└── ~System/
    └── (system tables)
```

## After DELETE

```sql
DELETE FROM Users WHERE Id = '1';
```

### Tables/Users/Users.txt

```
D|1         Richard                                           richard@example.com
A|2         Alice                                             alice@example.com
```
