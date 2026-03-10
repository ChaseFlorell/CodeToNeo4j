UNWIND $filePaths AS filePath
MATCH (f:File {path:filePath})-[:DECLARES]->(s:Symbol)
DETACH DELETE s
