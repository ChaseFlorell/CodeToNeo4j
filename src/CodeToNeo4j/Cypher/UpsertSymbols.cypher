UNWIND $symbols AS s
MERGE (n:Symbol {key: s.key})
SET n.name = s.name,
n.kind = s.kind,
n.class = s.class,
n.fqn = s.fqn,
n.accessibility = s.accessibility,
n.fileKey = s.fileKey,
n.filePath = s.filePath,
n.namespace = s.namespace,
n.startLine = s.startLine,
n.endLine = s.endLine,
n.documentation = s.documentation,
n.comments = s.comments,
n.version = s.version,
n.language = s.language,
n.technology = s.technology,
n.target_frameworks = coalesce(n.target_frameworks, []),
n.updatedAt = datetime(),
n.CodeToNeo4j = true
WITH n, s
MATCH (f:File {key: s.fileKey})
MERGE (f)-[:DECLARES]->(n)
