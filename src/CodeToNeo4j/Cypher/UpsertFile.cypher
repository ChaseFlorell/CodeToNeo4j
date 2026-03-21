UNWIND $files AS file
MERGE (f:File {key: file.fileKey})
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
f.target_frameworks = file.targetFrameworks,
f.CodeToNeo4j = true
WITH f, file
OPTIONAL MATCH (p:Project {key: file.repoKey})
	WHERE file.repoKey IS NOT NULL
FOREACH (ignoreMe IN CASE WHEN p IS NOT NULL THEN [1]
	ELSE []
	END |
	MERGE (p)-[:HAS_FILE]->(f)
)
WITH f, file
UNWIND (CASE WHEN file.authors IS NULL OR size(file.authors) = 0 THEN [null]
	ELSE file.authors
	END) AS author
WITH f, author
	WHERE author IS NOT NULL
MERGE (a:Author {name: author.name})
SET a.CodeToNeo4j = true
MERGE (a)-[r:AUTHORED]->(f)
SET r.firstCommit = datetime(author.firstCommit),
r.lastCommit = datetime(author.lastCommit),
r.commitCount = author.commitCount
