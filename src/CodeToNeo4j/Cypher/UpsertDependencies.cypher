UNWIND $dependencies AS d
MERGE (dep:Dependency {key:d.key})
SET dep.name = d.name,
    dep.version = d.version,
    dep.updatedAt = datetime()
WITH dep, d
MATCH (p:Project {key:d.repoKey})
MERGE (p)-[:DEPENDS_ON]->(dep)
