// Per-project size report: node counts by label, sorted largest first.
// symbolCount and authorCount use RETURN DISTINCT because a partial class/method can be
// DECLARES-ed by multiple src__File nodes into the same src__Symbol, and every src__Commit
// has exactly one src__Author — without DISTINCT both counts would be path counts, not node counts
// (symbolCount would over-count shared symbols, authorCount would degenerate into commitCount).
// dependencyCount is a footprint, not exclusive ownership: a NuGet package referenced by several
// projects is counted once per project, so totals across all projects overstate what --purge-data
// could actually reclaim for a single repo key (PurgeData.cypher only deletes a Dependency once no
// other project references it).
MATCH (p:src__Project)
	WHERE p.CodeToNeo4j = true
WITH p,
	COUNT { (p)-[:src__HAS_FILE]->(:src__File) } AS fileCount,
	COUNT { MATCH (p)-[:src__HAS_FILE]->(:src__File)-[:src__DECLARES]->(s:src__Symbol) RETURN DISTINCT s } AS symbolCount,
	COUNT { (p)-[:src__DEPENDS_ON]->(:src__Dependency) } AS dependencyCount,
	COUNT { (:src__Commit)-[:src__PART_OF_PROJECT]->(p) } AS commitCount,
	COUNT { MATCH (a:src__Author)-[:src__COMMITTED]->(:src__Commit)-[:src__PART_OF_PROJECT]->(p) RETURN DISTINCT a } AS authorCount
RETURN p.key AS repoKey, fileCount, symbolCount, dependencyCount, commitCount, authorCount,
	(1 + fileCount + symbolCount + dependencyCount + commitCount + authorCount) AS totalCount
ORDER BY totalCount DESC
