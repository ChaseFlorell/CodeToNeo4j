// Batch-friendly deletion for project-specific or full database purging.
CALL {
// 1. Symbols
MATCH (n:src__Symbol)
	WHERE n.CodeToNeo4j = true
	AND (
	$repoKey IS NULL
	OR exists {(:src__Project {key: $repoKey})-[:src__HAS_FILE]->(:src__File)-[:src__DECLARES]->(n)}
	OR NOT exists {(:src__Project)-[:src__HAS_FILE]->(:src__File)-[:src__DECLARES]->(n)}
	)
	AND ($extensions IS NULL OR any(ext IN $extensions
		WHERE n.filePath ENDS WITH ext))
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 2. Files
MATCH (n:src__File)
	WHERE n.CodeToNeo4j = true
	AND (
	$repoKey IS NULL
	OR exists {(:src__Project {key: $repoKey})-[:src__HAS_FILE]->(n)}
	OR NOT exists {(:src__Project)-[:src__HAS_FILE]->(n)}
	)
	AND ($extensions IS NULL OR any(ext IN $extensions
		WHERE n.path ENDS WITH ext))
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 3. Dependencies
MATCH (n:src__Dependency)
	WHERE n.CodeToNeo4j = true
	AND $purgeDependencies
	AND $extensions IS NULL
	AND (
	$repoKey IS NULL
	OR (exists {(:src__Project {key: $repoKey})-[:src__DEPENDS_ON]->(n)}
	AND NOT exists {MATCH (otherProject:src__Project)-[:src__DEPENDS_ON]->(n)
		WHERE otherProject.key <> $repoKey})
	)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 4. Commits
MATCH (n:src__Commit)
	WHERE n.CodeToNeo4j = true
	AND $extensions IS NULL
	AND (
	$repoKey IS NULL
	OR exists {(n)-[:src__PART_OF_PROJECT]->(:src__Project {key: $repoKey})}
	OR NOT exists {(n)-[:src__PART_OF_PROJECT]->(:src__Project)}
	)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 5. Authors
MATCH (n:src__Author)
	WHERE n.CodeToNeo4j = true
	AND $extensions IS NULL
	AND (
	$repoKey IS NULL
	OR (exists {(n)-[:src__COMMITTED]->(:src__Commit)-[:src__PART_OF_PROJECT]->(:src__Project {key: $repoKey})}
	AND NOT exists {(n)-[:src__COMMITTED]->(:src__Commit)-[:src__PART_OF_PROJECT]->(otherProject:src__Project)
		WHERE otherProject.key <> $repoKey})
	OR NOT exists {(n)-[:src__COMMITTED]->(:src__Commit)}
	)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 6. Projects
MATCH (n:src__Project)
	WHERE n.CodeToNeo4j = true
	AND $extensions IS NULL
	AND ($repoKey IS NULL OR n.key = $repoKey)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

}
RETURN coalesce(sum(count), 0) AS total
