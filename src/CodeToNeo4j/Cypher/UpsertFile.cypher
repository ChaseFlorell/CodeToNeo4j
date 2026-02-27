MERGE (f:File {key:$fileKey})
SET f.path=$path, f.hash=$hash, f.updatedAt=datetime()
WITH f
MATCH (p:Project {key:$repoKey})
MERGE (p)-[:HAS_FILE]->(f)
