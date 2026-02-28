MATCH (f:File {key:$fileKey})
OPTIONAL MATCH (f)-[:DECLARES]->(s:Symbol)
DETACH DELETE s, f
