// Pre-create all unique TargetFramework nodes once, then link files and symbols.
// This avoids redundant MERGE operations from the cartesian product of items × tfms × symbolKeys.

// 1. Create TargetFramework nodes (deduplicated by MERGE)
UNWIND $tfmNames AS tfmName
MERGE (tfm:TargetFramework {name: tfmName})
SET tfm.CodeToNeo4j = true
WITH COUNT(*)
AS _ignore

// 2. Link files to their target frameworks
UNWIND $items AS item
MATCH (f:File {key: item.fileKey})
UNWIND item.tfms AS tfmName
MATCH (tfm:TargetFramework {name: tfmName})
MERGE (f)-[:TARGETS_FRAMEWORK]->(tfm)
WITH COUNT(*)
AS _ignore

// 3. Link symbols to their target frameworks
UNWIND $symbolTfms AS st
MATCH (s:Symbol {key: st.symbolKey})
MATCH (tfm:TargetFramework {name: st.tfm})
MERGE (s)-[:TARGETS_FRAMEWORK]->(tfm)
