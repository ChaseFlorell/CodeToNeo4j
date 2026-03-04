UNWIND $files AS file
MERGE (f:File {key:file.fileKey})
SET f.path=file.path, f.hash=file.hash, f.updatedAt=datetime(), f.created=datetime(file.created), f.lastModified=datetime(file.lastModified), f.deleted=null, f.deletedAt=null, f.commits=file.commits, f.tags=file.tags, f.CodeToNeo4j=true
WITH f, file
OPTIONAL MATCH (p:Project {key:file.repoKey})
WHERE file.repoKey IS NOT NULL
FOREACH (ignoreMe IN CASE WHEN p IS NOT NULL THEN [1] ELSE [] END | MERGE (p)-[:HAS_FILE]->(f))
WITH f, file
UNWIND file.authors AS author
MERGE (a:Author {name: author.name})
MERGE (a)-[r:AUTHORED]->(f)
SET r.firstCommit=datetime(author.firstCommit), r.lastCommit=datetime(author.lastCommit), r.commitCount=author.commitCount
