# Sample Wiki Database

A simplified Wiki schema for use as a sample database. Contains tables for pages, content, user management, and image management. No front-end is provided; use the CLI to work with this database.

## Purpose

- Demonstrate SQL.txt with a realistic schema
- Provide sample scripts for documentation and testing
- Serve as a future data source for a Wiki website (separate repo)

## Schema Overview

| Table | Purpose |
|-------|---------|
| User | User accounts (id, username, email, created) |
| Page | Wiki pages (id, title, slug, created_by, updated) |
| PageContent | Content revisions (id, page_id, content, version, created_by) |
| Image | Image metadata (id, filename, mime_type, uploaded_by) |
| PageImage | Links pages to images |

## Table Definitions

### User

```sql
CREATE TABLE User (
    Id CHAR(10),
    Username CHAR(50),
    Email CHAR(100),
    CreatedAt CHAR(24)
);
```

### Page

```sql
CREATE TABLE Page (
    Id CHAR(10),
    Title CHAR(200),
    Slug CHAR(200),
    CreatedById CHAR(10),
    CreatedAt CHAR(24),
    UpdatedAt CHAR(24)
);
```

### PageContent

```sql
CREATE TABLE PageContent (
    Id CHAR(10),
    PageId CHAR(10),
    Content CHAR(5000),
    Version INT,
    CreatedById CHAR(10),
    CreatedAt CHAR(24)
);
```

### Image

```sql
CREATE TABLE Image (
    Id CHAR(10),
    Filename CHAR(255),
    MimeType CHAR(50),
    UploadedById CHAR(10),
    CreatedAt CHAR(24)
);
```

### PageImage

```sql
CREATE TABLE PageImage (
    Id CHAR(10),
    PageId CHAR(10),
    ImageId CHAR(10),
    Caption CHAR(200)
);
```

## Generating the Sample Database

### Step 1: Create the database

```bash
sqltxt create-db ./WikiDb
```

### Step 2: Run the schema script

```bash
sqltxt script --db ./WikiDb docs/samples/wiki-database/create-wiki.sql
```

### Step 3: (Optional) Run the seed script

```bash
sqltxt script --db ./WikiDb docs/samples/wiki-database/seed-wiki.sql
```

## Sample CLI Calls

### Create schema

```bash
sqltxt exec --db ./WikiDb "CREATE TABLE User (Id CHAR(10), Username CHAR(50), Email CHAR(100), CreatedAt CHAR(24));"
sqltxt exec --db ./WikiDb "CREATE TABLE Page (Id CHAR(10), Title CHAR(200), Slug CHAR(200), CreatedById CHAR(10), CreatedAt CHAR(24), UpdatedAt CHAR(24));"
# ... (or use script)
```

### Insert sample user

```bash
sqltxt exec --db ./WikiDb "INSERT INTO User (Id, Username, Email, CreatedAt) VALUES ('1', 'admin', 'admin@wiki.local', '2026-03-17T12:00:00Z');"
```

### Insert sample page

```bash
sqltxt exec --db ./WikiDb "INSERT INTO Page (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES ('1', 'Home', 'home', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z');"
```

### Query pages

```bash
sqltxt query --db ./WikiDb "SELECT * FROM Page;"
sqltxt query --db ./WikiDb "SELECT Id, Title, Slug FROM Page WHERE Id = '1';"
```

## Future: Wiki Website

A separate repository may create a simple Wiki website that uses SQL.txt and this sample database. The schema is designed to support:

- Pages with revision history (PageContent)
- User attribution (CreatedById, UploadedById)
- Image attachments (Image, PageImage)

## File Locations

| File | Purpose |
|------|---------|
| `docs/samples/wiki-database/create-wiki.sql` | Schema creation script |
| `docs/samples/wiki-database/seed-wiki.sql` | Sample data script |
| `docs/samples/wiki-database/README.md` | This document |
