UNWIND $symbols AS s
MERGE (n:Symbol {key:s.key})
SET n.name = s.name,
    n.kind = s.kind,
    n.fqn = s.fqn,
    n.accessibility = s.accessibility,
    n.fileKey = s.fileKey,
    n.filePath = s.filePath,
    n.startLine = s.startLine,
    n.endLine = s.endLine,
    n.documentation = s.documentation,
    n.comments = s.comments,
    n.updatedAt = datetime()
WITH n, s
MATCH (f:File {key:s.fileKey})
MERGE (f)-[:DECLARES]->(n)
