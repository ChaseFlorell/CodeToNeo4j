UNWIND $items AS item
UNWIND item.tfms AS tfmName
MERGE (tfm:TargetFramework {name: tfmName})
SET tfm.CodeToNeo4j = true
WITH tfm, item
MATCH (f:File {key: item.fileKey})
MERGE (f)-[:TARGETS_FRAMEWORK]->(tfm)
WITH tfm, item
UNWIND item.symbolKeys AS sk
MATCH (s:Symbol {key: sk})
MERGE (s)-[:TARGETS_FRAMEWORK]->(tfm)
