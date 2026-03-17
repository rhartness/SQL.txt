-- SQL.txt Sample: Seed Wiki Database with Sample Data
-- Note: Use \n, \r, \t, \\ in string literals for newline, CR, tab, backslash

INSERT INTO User (Id, Username, Email, CreatedAt) VALUES ('1', 'admin', 'admin@wiki.local', '2026-03-17T12:00:00Z');
INSERT INTO User (Id, Username, Email, CreatedAt) VALUES ('2', 'editor', 'editor@wiki.local', '2026-03-17T12:01:00Z');

INSERT INTO Page (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES ('1', 'Home', 'home', '1', '2026-03-17T12:00:00Z', '2026-03-17T12:00:00Z');
INSERT INTO Page (Id, Title, Slug, CreatedById, CreatedAt, UpdatedAt) VALUES ('2', 'About', 'about', '1', '2026-03-17T12:05:00Z', '2026-03-17T12:05:00Z');

INSERT INTO PageContent (Id, PageId, Content, Version, CreatedById, CreatedAt) VALUES ('1', '1', 'Welcome to the sample Wiki.\n\nThis is the home page.', 1, '1', '2026-03-17T12:00:00Z');
INSERT INTO PageContent (Id, PageId, Content, Version, CreatedById, CreatedAt) VALUES ('2', '2', 'About this Wiki.\n\tBuilt with SQL.txt.', 1, '1', '2026-03-17T12:05:00Z');
