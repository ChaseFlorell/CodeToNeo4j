// Batch-friendly deletion for project-specific or full database purging.
CALL {
// 1. Symbols
MATCH (n:Symbol)
	WHERE n.CodeToNeo4j = true
	AND (
	$repoKey IS NULL
	OR exists {(:Project {key: $repoKey})-[:HAS_FILE]->(:File)-[:DECLARES]->(n)}
	OR NOT exists {(:Project)-[:HAS_FILE]->(:File)-[:DECLARES]->(n)}
	)
	AND ($extensions IS NULL OR any(ext IN $extensions
		WHERE n.filePath ENDS WITH ext))
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 2. Files
MATCH (n:File)
	WHERE n.CodeToNeo4j = true
	AND (
	$repoKey IS NULL
	OR exists {(:Project {key: $repoKey})-[:HAS_FILE]->(n)}
	OR NOT exists {(:Project)-[:HAS_FILE]->(n)}
	)
	AND ($extensions IS NULL OR any(ext IN $extensions
		WHERE n.path ENDS WITH ext))
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 3. Dependencies
MATCH (n:Dependency)
	WHERE n.CodeToNeo4j = true
	AND $purgeDependencies
	AND $extensions IS NULL
	AND (
	$repoKey IS NULL
	OR (exists {(:Project {key: $repoKey})-[:DEPENDS_ON]->(n)}
	AND NOT exists {MATCH (otherProject:Project)-[:DEPENDS_ON]->(n)
		WHERE otherProject.key <> $repoKey})
	)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 4. Commits
MATCH (n:Commit)
	WHERE n.CodeToNeo4j = true
	AND $extensions IS NULL
	AND (
	$repoKey IS NULL
	OR exists {(n)-[:PART_OF_PROJECT]->(:Project {key: $repoKey})}
	OR NOT exists {(n)-[:PART_OF_PROJECT]->(:Project)}
	)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 5. Authors
MATCH (n:Author)
	WHERE n.CodeToNeo4j = true
	AND $extensions IS NULL
	AND (
	$repoKey IS NULL
	OR (exists {(n)-[:COMMITTED]->(:Commit)-[:PART_OF_PROJECT]->(:Project {key: $repoKey})}
	AND NOT exists {(n)-[:COMMITTED]->(:Commit)-[:PART_OF_PROJECT]->(otherProject:Project)
		WHERE otherProject.key <> $repoKey})
	OR NOT exists {(n)-[:COMMITTED]->(:Commit)}
	)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 6. Projects
MATCH (n:Project)
	WHERE n.CodeToNeo4j = true
	AND $extensions IS NULL
	AND ($repoKey IS NULL OR n.key = $repoKey)
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count

UNION ALL

// 7. Orphaned TargetFrameworks
MATCH (n:TargetFramework)
	WHERE n.CodeToNeo4j = true
	AND $extensions IS NULL
	AND NOT exists {()-[:TARGETS_FRAMEWORK]->(n)}
WITH n
	LIMIT $batchSize
DETACH DELETE n
RETURN count(n) AS count
}
RETURN coalesce(sum(count), 0) AS total
