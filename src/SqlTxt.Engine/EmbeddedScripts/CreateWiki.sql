-- SQL.txt Sample: Create Wiki Database Schema

CREATE TABLE User (
    Id CHAR(10),
    Username CHAR(50),
    Email CHAR(100),
    CreatedAt CHAR(24)
);

CREATE TABLE Page (
    Id CHAR(10),
    Title CHAR(200),
    Slug CHAR(200),
    CreatedById CHAR(10),
    CreatedAt CHAR(24),
    UpdatedAt CHAR(24)
);

CREATE TABLE PageContent (
    Id CHAR(10),
    PageId CHAR(10),
    Content CHAR(5000),
    Version INT,
    CreatedById CHAR(10),
    CreatedAt CHAR(24)
);

CREATE TABLE Image (
    Id CHAR(10),
    Filename CHAR(255),
    MimeType CHAR(50),
    UploadedById CHAR(10),
    CreatedAt CHAR(24)
);

CREATE TABLE PageImage (
    Id CHAR(10),
    PageId CHAR(10),
    ImageId CHAR(10),
    Caption CHAR(200)
);
