MATCH (f:src__File {path: $filePath})
SET f.deleted = true, f.deletedAt = datetime()
WITH f
OPTIONAL MATCH (f)-[:src__DECLARES]->(s:src__Symbol)
SET s.deleted = true, s.deletedAt = datetime()
