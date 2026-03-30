UNWIND $files AS file
MERGE (f:src__File {key: file.fileKey})
SET f.path = file.path,
f.fileName = file.fileName,
f.namespace = file.namespace,
f.hash = file.hash,
f.updatedAt = datetime(),
f.created = datetime(file.created),
f.lastModified = datetime(file.lastModified),
f.deleted = false,
f.deletedAt = null,
f.commits = file.commits,
f.tags = file.tags,
f.language = file.language,
f.technology = file.technology,
f.CodeToNeo4j = true
WITH f, file
OPTIONAL MATCH (p:src__Project {key: file.repoKey})
	WHERE file.repoKey IS NOT NULL
FOREACH (ignoreMe IN CASE WHEN p IS NOT NULL THEN [1]
	ELSE []
	END |
	MERGE (p)-[:src__HAS_FILE]->(f)
)
WITH f, file
UNWIND (CASE WHEN file.authors IS NULL OR size(file.authors) = 0 THEN [null]
	ELSE file.authors
	END) AS author
WITH f, author
	WHERE author IS NOT NULL
MERGE (a:src__Author {name: author.name})
SET a.CodeToNeo4j = true
MERGE (a)-[r:src__AUTHORED]->(f)
SET r.firstCommit = datetime(author.firstCommit),
r.lastCommit = datetime(author.lastCommit),
r.commitCount = author.commitCount
