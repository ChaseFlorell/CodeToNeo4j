// Per-project size report: node counts by label, sorted largest first.
MATCH (p:src__Project)
	WHERE p.CodeToNeo4j = true
WITH p,
	COUNT { (p)-[:src__HAS_FILE]->(:src__File) } AS fileCount,
	COUNT { (p)-[:src__HAS_FILE]->(:src__File)-[:src__DECLARES]->(:src__Symbol) } AS symbolCount,
	COUNT { (p)-[:src__DEPENDS_ON]->(:src__Dependency) } AS dependencyCount,
	COUNT { (:src__Commit)-[:src__PART_OF_PROJECT]->(p) } AS commitCount,
	COUNT { (:src__Author)-[:src__COMMITTED]->(:src__Commit)-[:src__PART_OF_PROJECT]->(p) } AS authorCount
RETURN p.key AS repoKey, p.name AS name, fileCount, symbolCount, dependencyCount, commitCount, authorCount,
	(1 + fileCount + symbolCount + dependencyCount + commitCount + authorCount) AS totalCount
ORDER BY totalCount DESC
