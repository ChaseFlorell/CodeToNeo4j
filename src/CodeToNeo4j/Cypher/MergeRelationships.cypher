UNWIND $rels AS r
MATCH (a:src__Symbol {key: r.fromKey})
MATCH (b:src__Symbol {key: r.toKey})
CALL apoc.merge.relationship(a, r.relType, {}, {}, b, {}) YIELD rel
RETURN COUNT(*) AS created
