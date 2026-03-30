UNWIND $urls AS u
MERGE (url:src__Url {key: u.urlKey})
SET url.name = u.name,
url.updatedAt = datetime(),
url.CodeToNeo4j = true
WITH url, u
MATCH (dep:src__Dependency {key: u.depKey})
MERGE (dep)-[:src__HAS_URL]->(url)
