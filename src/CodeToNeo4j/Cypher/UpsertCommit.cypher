UNWIND $commits AS commit
MERGE (c:Commit {hash: commit.hash})
SET c.date = datetime(commit.date), c.message = commit.message
WITH c, commit
MATCH (p:Project {key: commit.repoKey})
MERGE (c)-[:PART_OF_PROJECT]->(p)
WITH c, commit
MERGE (a:Author {name: commit.authorName})
ON CREATE SET a.email = commit.authorEmail
MERGE (a)-[:COMMITTED]->(c)
WITH c, commit
UNWIND commit.changedFiles AS fileKey
MATCH (f:File {key: fileKey})
MERGE (c)-[:MODIFIED_FILE]->(f)
