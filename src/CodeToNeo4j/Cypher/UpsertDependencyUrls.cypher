UNWIND $urls AS u
MERGE (url:Url {key: u.urlKey})
SET url.name = u.name,
    url.updatedAt = datetime(),
    url.CodeToNeo4j = true
WITH url, u
MATCH (dep:Dependency {key: u.depKey})
MERGE (dep)-[:HAS_URL]->(url)
