# CLI Reference

> **Status:** Placeholder. This document will be updated as the CLI is implemented.

## Overview

The SQL.txt CLI (`sqltxt`) provides command-line access to create databases, execute SQL.txt commands, and inspect metadata.

## Commands

### create-db

Creates a new database directory.

**Syntax**

```bash
sqltxt create-db <path>
```

**Examples**

```bash
sqltxt create-db ./MyDatabase
sqltxt create-db /data/wiki
```

**Exit codes**

- `0` — Success
- `1` — Error (path exists, permission denied, etc.)

---

### exec

Executes a single SQL.txt statement.

**Syntax**

```bash
sqltxt exec --db <path> "<statement>"
```

**Examples**

```bash
sqltxt exec --db ./MyDatabase "CREATE TABLE Users (Id CHAR(10), Name CHAR(50));"
sqltxt exec --db ./MyDatabase "INSERT INTO Users (Id, Name) VALUES ('1', 'Alice');"
```

---

### query

Executes a SELECT statement and prints results.

**Syntax**

```bash
sqltxt query --db <path> "<select-statement>"
```

**Examples**

```bash
sqltxt query --db ./MyDatabase "SELECT * FROM Users;"
sqltxt query --db ./MyDatabase "SELECT Id, Name FROM Users WHERE Id = '1';"
```

---

### script

Executes a file containing SQL.txt statements.

**Syntax**

```bash
sqltxt script --db <path> <script-file>
```

**Examples**

```bash
sqltxt script --db ./WikiDb docs/samples/wiki-database/create-wiki.sql
```

---

### inspect

Prints database metadata (tables, columns, row counts).

**Syntax**

```bash
sqltxt inspect --db <path> [--tables | --schema <table>]
```

---

## Common Workflows

### Create and populate a database

```bash
sqltxt create-db ./DemoDb
sqltxt exec --db ./DemoDb "CREATE TABLE Users (Id CHAR(10), Name CHAR(50));"
sqltxt exec --db ./DemoDb "INSERT INTO Users (Id, Name) VALUES ('1', 'Alice');"
sqltxt query --db ./DemoDb "SELECT * FROM Users;"
```

### Run a setup script

```bash
sqltxt create-db ./WikiDb
sqltxt script --db ./WikiDb docs/samples/wiki-database/create-wiki.sql
```
