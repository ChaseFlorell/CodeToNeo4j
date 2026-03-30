UNWIND $fileKeys AS fileKey
MATCH (f:src__File {key: fileKey})-[:src__DECLARES]->(s:src__Symbol)
DETACH DELETE s
