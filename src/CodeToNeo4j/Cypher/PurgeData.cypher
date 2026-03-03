MATCH (p:Project {key: $repoKey})
OPTIONAL MATCH (p)-[:HAS_FILE]->(f:File)
WHERE $extensions IS NULL OR any(ext IN $extensions WHERE f.path ENDS WITH ext)
OPTIONAL MATCH (f)-[:DECLARES]->(s:Symbol)
DETACH DELETE s, f
WITH p
WHERE $extensions IS NULL
OPTIONAL MATCH (c:Commit)-[:PART_OF_PROJECT]->(p)
DETACH DELETE c, p
