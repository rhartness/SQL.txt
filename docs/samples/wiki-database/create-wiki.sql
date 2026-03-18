-- SQL.txt Sample: Create Wiki Database Schema (Phase 2: PK/FK)
-- Run with: sqltxt script --db ./WikiDb docs/samples/wiki-database/create-wiki.sql
-- Prerequisite: Database must already exist (sqltxt create-db ./WikiDb)

CREATE TABLE User (
    Id CHAR(10) PRIMARY KEY,
    Username CHAR(50),
    Email CHAR(100),
    CreatedAt CHAR(24)
);

CREATE TABLE Page (
    Id CHAR(10) PRIMARY KEY,
    Title CHAR(200),
    Slug CHAR(200),
    CreatedById CHAR(10),
    CreatedAt CHAR(24),
    UpdatedAt CHAR(24),
    FOREIGN KEY (CreatedById) REFERENCES User(Id)
);

CREATE TABLE PageContent (
    Id CHAR(10) PRIMARY KEY,
    PageId CHAR(10),
    Content CHAR(5000),
    Version INT,
    CreatedById CHAR(10),
    CreatedAt CHAR(24),
    FOREIGN KEY (PageId) REFERENCES Page(Id),
    FOREIGN KEY (CreatedById) REFERENCES User(Id)
);

CREATE TABLE Image (
    Id CHAR(10) PRIMARY KEY,
    Filename CHAR(255),
    MimeType CHAR(50),
    UploadedById CHAR(10),
    CreatedAt CHAR(24),
    FOREIGN KEY (UploadedById) REFERENCES User(Id)
);

CREATE TABLE PageImage (
    Id CHAR(10) PRIMARY KEY,
    PageId CHAR(10),
    ImageId CHAR(10),
    Caption CHAR(200),
    FOREIGN KEY (PageId) REFERENCES Page(Id),
    FOREIGN KEY (ImageId) REFERENCES Image(Id)
);

-- Indexes for common lookups
CREATE INDEX IX_User_Username ON User(Username);
CREATE INDEX IX_Page_Slug ON Page(Slug);
CREATE INDEX IX_Page_CreatedById ON Page(CreatedById);
CREATE INDEX IX_PageContent_PageId ON PageContent(PageId);
CREATE INDEX IX_Image_UploadedById ON Image(UploadedById);
CREATE INDEX IX_PageImage_PageId ON PageImage(PageId);
CREATE INDEX IX_PageImage_ImageId ON PageImage(ImageId);
