UNWIND $dependencies AS d
MERGE (dep:Dependency {key:d.key})
SET dep.name = d.name,
    dep.version = d.version,
    dep.updatedAt = datetime(),
    dep.CodeToNeo4j = true
WITH dep, d
OPTIONAL MATCH (p:Project {key:d.repoKey})
WHERE d.repoKey IS NOT NULL
FOREACH (ignoreMe IN CASE WHEN p IS NOT NULL THEN [1] ELSE [] END | MERGE (p)-[:DEPENDS_ON]->(dep))
