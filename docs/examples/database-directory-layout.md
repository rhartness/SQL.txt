# Example Database Directory Layout

This document shows the on-disk structure after creating a database and table with sample data.

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
├── db.manifest.json
├── system/
│   ├── tables.meta.txt
│   ├── columns.meta.txt
│   └── engine.meta.json
└── tables/
    └── Users/
        ├── schema.txt
        ├── data.txt
        └── table.meta.txt
```

## File Contents

### db.manifest.json

```json
{
  "engineVersion": "0.1.0",
  "storageFormatVersion": 1
}
```

### system/tables.meta.txt

```
Users|2026-03-16T00:00:00Z
```

### system/columns.meta.txt

```
Users|1|Id|CHAR|10
Users|2|Name|CHAR|50
Users|3|Email|CHAR|100
```

### tables/Users/schema.txt

```
TABLE: Users
FORMAT_VERSION: 1
COLUMNS:
1|Id|CHAR|10
2|Name|CHAR|50
3|Email|CHAR|100
```

### tables/Users/data.txt

```
A|1         Richard                                           richard@example.com
A|2         Alice                                             alice@example.com
```

### tables/Users/table.meta.txt

```
TABLE: Users
ROW_COUNT: 2
ACTIVE_ROW_COUNT: 2
DELETED_ROW_COUNT: 0
LAST_UPDATED_UTC: 2026-03-16T00:00:00Z
```

## After DELETE

```sql
DELETE FROM Users WHERE Id = '1';
```

### tables/Users/data.txt

```
D|1         Richard                                           richard@example.com
A|2         Alice                                             alice@example.com
```

### tables/Users/table.meta.txt

```
TABLE: Users
ROW_COUNT: 2
ACTIVE_ROW_COUNT: 1
DELETED_ROW_COUNT: 1
LAST_UPDATED_UTC: 2026-03-16T00:01:00Z
```
