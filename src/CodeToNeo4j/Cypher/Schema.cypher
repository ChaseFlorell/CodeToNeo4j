// === Constraints ===
CREATE CONSTRAINT src__project_key IF NOT EXISTS
FOR (p:src__Project) REQUIRE p.key IS UNIQUE;

CREATE CONSTRAINT src__file_key IF NOT EXISTS
FOR (f:src__File) REQUIRE f.key IS UNIQUE;

CREATE CONSTRAINT src__symbol_key IF NOT EXISTS
FOR (s:src__Symbol) REQUIRE s.key IS UNIQUE;

CREATE CONSTRAINT src__dependency_key IF NOT EXISTS
FOR (d:src__Dependency) REQUIRE d.key IS UNIQUE;

CREATE CONSTRAINT src__author_name IF NOT EXISTS
FOR (a:src__Author) REQUIRE a.name IS UNIQUE;

CREATE CONSTRAINT src__commit_hash IF NOT EXISTS
FOR (c:src__Commit) REQUIRE c.hash IS UNIQUE;

CREATE CONSTRAINT src__tag_name IF NOT EXISTS
FOR (t:src__Tag) REQUIRE t.name IS UNIQUE;

CREATE CONSTRAINT src__url_key IF NOT EXISTS
FOR (u:src__Url) REQUIRE u.key IS UNIQUE;

// === Standard Indexes ===
CREATE INDEX src__symbol_name IF NOT EXISTS
FOR (s:src__Symbol) ON (s.name);

CREATE INDEX src__symbol_kind IF NOT EXISTS
FOR (s:src__Symbol) ON (s.kind);

CREATE INDEX src__symbol_fqn IF NOT EXISTS
FOR (s:src__Symbol) ON (s.fqn);

CREATE INDEX src__file_path IF NOT EXISTS
FOR (f:src__File) ON (f.path);

CREATE INDEX src__dependency_name IF NOT EXISTS
FOR (d:src__Dependency) ON (d.name);

CREATE INDEX src__commit_date IF NOT EXISTS
FOR (c:src__Commit) ON (c.date);

CREATE INDEX src__url_name IF NOT EXISTS
FOR (u:src__Url) ON (u.name);

// === Composite Indexes ===
CREATE INDEX src__symbol_file_kind IF NOT EXISTS
FOR (s:src__Symbol) ON (s.fileKey, s.kind);

// === TEXT Indexes (no 8KB limit) ===
DROP INDEX symbol_documentation IF EXISTS;
DROP INDEX symbol_comments IF EXISTS;

CREATE TEXT INDEX src__symbol_documentation IF NOT EXISTS
FOR (s:src__Symbol) ON (s.documentation);

CREATE TEXT INDEX src__symbol_comments IF NOT EXISTS
FOR (s:src__Symbol) ON (s.comments);
