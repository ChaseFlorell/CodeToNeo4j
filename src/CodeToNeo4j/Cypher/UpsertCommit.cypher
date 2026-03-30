UNWIND $commits AS commit
MERGE (c:src__Commit {hash: commit.hash})
SET c.date = datetime(commit.date), c.message = commit.message, c.author = commit.authorName, c.CodeToNeo4j = true
WITH c, commit
OPTIONAL MATCH (p:src__Project {key: commit.repoKey})
	WHERE commit.repoKey IS NOT NULL
FOREACH (ignoreMe IN CASE WHEN p IS NOT NULL THEN [1]
	ELSE []
	END |
	MERGE (c)-[:src__PART_OF_PROJECT]->(p)
)
WITH c, commit
MERGE (a:src__Author {name: commit.authorName})
SET a.email = commit.authorEmail, a.CodeToNeo4j = true
MERGE (a)-[:src__COMMITTED]->(c)
WITH c, commit
UNWIND commit.changedFiles AS fileInfo
MERGE (f:src__File {key: fileInfo.key})
SET f.path = fileInfo.path, f.namespace = fileInfo.namespace, f.deleted = fileInfo.deleted, f.CodeToNeo4j = true
MERGE (c)-[:src__MODIFIED_FILE]->(f)
