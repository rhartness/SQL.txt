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

The schema uses Phase 2 features (primary keys, foreign keys, indexes) and Phase 3 VARCHAR for variable-length content.

### User

```sql
CREATE TABLE User (
    Id CHAR(10) PRIMARY KEY,
    Username CHAR(50),
    Email CHAR(100),
    CreatedAt CHAR(24)
);
```

### Page

```sql
CREATE TABLE Page (
    Id CHAR(10) PRIMARY KEY,
    Title CHAR(200),
    Slug CHAR(200),
    CreatedById CHAR(10),
    CreatedAt CHAR(24),
    UpdatedAt CHAR(24),
    FOREIGN KEY (CreatedById) REFERENCES User(Id)
);
```

### PageContent

```sql
CREATE TABLE PageContent (
    Id CHAR(10) PRIMARY KEY,
    PageId CHAR(10),
    Content VARCHAR(5000),
    Version INT,
    CreatedById CHAR(10),
    CreatedAt CHAR(24),
    FOREIGN KEY (PageId) REFERENCES Page(Id),
    FOREIGN KEY (CreatedById) REFERENCES User(Id)
);
```

### Image

```sql
CREATE TABLE Image (
    Id CHAR(10) PRIMARY KEY,
    Filename CHAR(255),
    MimeType CHAR(50),
    UploadedById CHAR(10),
    CreatedAt CHAR(24),
    FOREIGN KEY (UploadedById) REFERENCES User(Id)
);
```

### PageImage

```sql
CREATE TABLE PageImage (
    Id CHAR(10) PRIMARY KEY,
    PageId CHAR(10),
    ImageId CHAR(10),
    Caption CHAR(200),
    FOREIGN KEY (PageId) REFERENCES Page(Id),
    FOREIGN KEY (ImageId) REFERENCES Image(Id)
);
```

### Indexes

```sql
CREATE INDEX IX_User_Username ON User(Username);
CREATE INDEX IX_Page_Slug ON Page(Slug);
CREATE INDEX IX_Page_CreatedById ON Page(CreatedById);
CREATE INDEX IX_PageContent_PageId ON PageContent(PageId);
CREATE INDEX IX_Image_UploadedById ON Image(UploadedById);
CREATE INDEX IX_PageImage_PageId ON PageImage(PageId);
CREATE INDEX IX_PageImage_ImageId ON PageImage(ImageId);
```

## Generating the Sample Database

**Preferred:** Use `build-sample-wiki` to create the database in one step. This is the canonical way to build the sample and is integrated into CLI, NuGet API, and Service.

### Option A: Build Sample Wiki (recommended)

**Filesystem storage:**
```bash
sqltxt build-sample-wiki --db .
```

**WASM storage (single `.wasmdb` file):**
```bash
sqltxt build-sample-wiki --db . --wasm
```

Creates `./WikiDb` (or `./WikiDb.wasmdb` with `--wasm`) with schema and seed data. Use `--db <path>` to specify the parent directory. Verbose output shows each step. The database is deleted and rebuilt if it already exists.

**API / NuGet:**
```csharp
await SqlTxtApi.BuildSampleWikiAsync(path, new BuildSampleWikiOptions(Verbose: false, DeleteIfExists: true));
```

**Service:** Set environment variable `SQLTXT_BUILD_SAMPLE_WIKI` to the parent path to build on startup.

### Option B: Manual steps

**Filesystem storage:**
```bash
sqltxt create-db ./WikiDb
sqltxt script --db ./WikiDb docs/samples/wiki-database/create-wiki.sql
sqltxt script --db ./WikiDb docs/samples/wiki-database/seed-wiki.sql
```

**WASM storage:**
```bash
sqltxt create-db ./WikiDb --wasm
sqltxt script --db ./WikiDb.wasmdb --wasm docs/samples/wiki-database/create-wiki.sql
sqltxt script --db ./WikiDb.wasmdb --wasm docs/samples/wiki-database/seed-wiki.sql
```

### Verify

**Filesystem:**
```bash
sqltxt query --db ./WikiDb "SELECT * FROM Page"
sqltxt query --db ./WikiDb "SELECT Id, PageId, Content FROM PageContent"
```

**WASM:**
```bash
sqltxt query --db ./WikiDb.wasmdb --wasm "SELECT * FROM Page"
sqltxt query --db ./WikiDb.wasmdb --wasm "SELECT Id, PageId, Content FROM PageContent"
```

The seed data demonstrates CHAR field encoding: `\n` (newline) and `\t` (tab) in string literals are stored and decoded correctly. For example, PageContent row 1 has `Welcome to the sample Wiki.\n\nThis is the home page.` which displays with actual line breaks when queried.

### Sample database lifecycle

When new features are added to SQL.txt, rebuild the sample database to observe them in the generated files. Run `build-sample-wiki` after schema or behavior changes.

## Sample CLI Calls

### Create schema

```bash
sqltxt exec --db ./WikiDb "CREATE TABLE User (Id CHAR(10) PRIMARY KEY, Username CHAR(50), Email CHAR(100), CreatedAt CHAR(24));"
sqltxt exec --db ./WikiDb "CREATE TABLE Page (Id CHAR(10) PRIMARY KEY, Title CHAR(200), Slug CHAR(200), CreatedById CHAR(10), CreatedAt CHAR(24), UpdatedAt CHAR(24), FOREIGN KEY (CreatedById) REFERENCES User(Id));"
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
