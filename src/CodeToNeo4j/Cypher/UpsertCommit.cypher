UNWIND $commits AS commit
MERGE (c:Commit {hash: commit.hash})
SET c.date = datetime(commit.date), c.message = commit.message, c.CodeToNeo4j = true
WITH c, commit
OPTIONAL MATCH (p:Project {key: commit.repoKey})
WHERE commit.repoKey IS NOT NULL
FOREACH (ignoreMe IN CASE WHEN p IS NOT NULL THEN [1] ELSE [] END | MERGE (c)-[:PART_OF_PROJECT]->(p))
WITH c, commit
MERGE (a:Author {name: commit.authorName})
SET a.email = commit.authorEmail, a.CodeToNeo4j = true
MERGE (a)-[:COMMITTED]->(c)
WITH c, commit
UNWIND commit.changedFiles AS fileInfo
MERGE (f:File {path: fileInfo.path})
ON CREATE SET f.key = fileInfo.key, f.namespace = fileInfo.namespace, f.deleted = true, f.CodeToNeo4j = true
MERGE (c)-[:MODIFIED_FILE]->(f)
