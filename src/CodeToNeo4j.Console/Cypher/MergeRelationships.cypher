UNWIND $rels AS r
MATCH (a:Symbol {key:r.fromKey})
MATCH (b:Symbol {key:r.toKey})
CALL {
  WITH a, b, r
  // Relationship type is fixed in v1, safe to switch later
  MERGE (a)-[:CONTAINS]->(b)
}
RETURN count(*) AS created
