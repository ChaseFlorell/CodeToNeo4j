MERGE (f:File {key:$fileKey})
SET f.path=$path, f.hash=$hash, f.updatedAt=datetime(), f.created=datetime($created), f.lastModified=datetime($lastModified), f.deleted=null, f.deletedAt=null, f.commits=$commits, f.tags=$tags
WITH f
MATCH (p:Project {key:$repoKey})
MERGE (p)-[:HAS_FILE]->(f)
WITH f
UNWIND $authors AS author
MERGE (a:Author {name: author.name})
MERGE (a)-[r:AUTHORED]->(f)
SET r.firstCommit=datetime(author.firstCommit), r.lastCommit=datetime(author.lastCommit), r.commitCount=author.commitCount
