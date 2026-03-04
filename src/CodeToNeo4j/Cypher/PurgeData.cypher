CALL {
    WITH $repoKey, $extensions
    WHERE $repoKey IS NOT NULL
    MATCH (p:Project {key: $repoKey})
    OPTIONAL MATCH (p)-[:HAS_FILE]->(f:File)
    WHERE $extensions IS NULL OR any(ext IN $extensions WHERE f.path ENDS WITH ext)
    OPTIONAL MATCH (f)-[:DECLARES]->(s:Symbol)
    DETACH DELETE s, f
    WITH p, $extensions
    WHERE $extensions IS NULL
    OPTIONAL MATCH (c:Commit)-[:PART_OF_PROJECT]->(p)
    DETACH DELETE c, p
    RETURN count(*) as count1
}
CALL {
    WITH $repoKey, $extensions
    WHERE $repoKey IS NULL
    MATCH (n {CodeToNeo4j: true})
    WHERE NOT n:Project OR $extensions IS NULL
    WITH n, $extensions
    WHERE NOT n:File OR $extensions IS NULL OR any(ext IN $extensions WHERE n.path ENDS WITH ext)
    DETACH DELETE n
    RETURN count(*) as count2
}
RETURN count1 + count2 as total
