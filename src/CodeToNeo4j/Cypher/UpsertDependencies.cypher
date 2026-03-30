UNWIND $dependencies AS d
MERGE (dep:src__Dependency {key: d.key})
SET dep.name = d.name,
dep.version = d.version,
dep.updatedAt = datetime(),
dep.CodeToNeo4j = true
WITH dep, d
OPTIONAL MATCH (p:src__Project {key: d.repoKey})
	WHERE d.repoKey IS NOT NULL
FOREACH (ignoreMe IN CASE WHEN p IS NOT NULL THEN [1]
	ELSE []
	END |
	MERGE (p)-[:src__DEPENDS_ON]->(dep)
)
