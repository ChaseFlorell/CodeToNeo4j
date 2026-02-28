CREATE CONSTRAINT project_key IF NOT EXISTS
FOR (p:Project) REQUIRE p.key IS UNIQUE;

CREATE CONSTRAINT file_key IF NOT EXISTS
FOR (f:File) REQUIRE f.key IS UNIQUE;

CREATE CONSTRAINT symbol_key IF NOT EXISTS
FOR (s:Symbol) REQUIRE s.key IS UNIQUE;

CREATE INDEX symbol_name IF NOT EXISTS
FOR (s:Symbol) ON (s.name);

CREATE INDEX symbol_kind IF NOT EXISTS
FOR (s:Symbol) ON (s.kind);

CREATE INDEX file_path IF NOT EXISTS
FOR (f:File) ON (f.path);

CREATE INDEX symbol_fqn IF NOT EXISTS
FOR (s:Symbol) ON (s.fqn);

CREATE INDEX symbol_fileKey IF NOT EXISTS
FOR (s:Symbol) ON (s.fileKey);

CREATE CONSTRAINT dependency_key IF NOT EXISTS
FOR (d:Dependency) REQUIRE d.key IS UNIQUE;

CREATE INDEX dependency_name IF NOT EXISTS
FOR (d:Dependency) ON (d.name);

// Full-text indexes for comments and documentation to support advanced search
CREATE INDEX symbol_documentation IF NOT EXISTS
FOR (s:Symbol) ON (s.documentation);

CREATE INDEX symbol_comments IF NOT EXISTS
FOR (s:Symbol) ON (s.comments);
  
CREATE CONSTRAINT author_name IF NOT EXISTS
FOR (a:Author) REQUIRE a.name IS UNIQUE;
