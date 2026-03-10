UNWIND $symbols AS s
MERGE (n:Symbol {key:s.key})
SET n.name = s.name,
    n.kind = s.kind,
    n.fqn = s.fqn,
    n.accessibility = s.accessibility,
    n.fileKey = s.fileKey,
    n.filePath = s.filePath,
    n.namespace = s.namespace,
    n.startLine = s.startLine,
    n.endLine = s.endLine,
    n.documentation = s.documentation,
    n.comments = s.comments,
    n.updatedAt = datetime(),
    n.CodeToNeo4j = true
WITH n, s
MATCH (f:File {path:s.filePath})
MERGE (f)-[:DECLARES]->(n)
