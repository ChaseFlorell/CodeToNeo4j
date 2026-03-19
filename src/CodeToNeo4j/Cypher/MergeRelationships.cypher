UNWIND $rels AS r
MATCH (a:Symbol {key: r.fromKey})
MATCH (b:Symbol {key: r.toKey})
CALL apoc.merge.relationship(a, r.relType, {}, {}, b, {}) YIELD rel
RETURN COUNT(*) AS created
