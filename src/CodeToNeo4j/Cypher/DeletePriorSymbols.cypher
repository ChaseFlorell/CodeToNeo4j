MATCH (f:File {key:$fileKey})-[:DECLARES]->(s:Symbol)
DETACH DELETE s
