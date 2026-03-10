MATCH (f:File {path:$filePath})
SET f.deleted = true, f.deletedAt = datetime()
WITH f
OPTIONAL MATCH (f)-[:DECLARES]->(s:Symbol)
SET s.deleted = true, s.deletedAt = datetime()
