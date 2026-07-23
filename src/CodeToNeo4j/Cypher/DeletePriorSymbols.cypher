UNWIND $fileKeys AS fileKey
MATCH (f:src__File {key: fileKey})-[:src__DECLARES]->(s:src__Symbol)
WHERE NOT s.key IN $keepKeys
DETACH DELETE s
