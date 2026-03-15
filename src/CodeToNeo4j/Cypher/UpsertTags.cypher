UNWIND $symbolTags AS st
MATCH (s:Symbol {key: st.symbolKey})
UNWIND st.tags AS tagName
MERGE (t:Tag {name: tagName})
SET t.CodeToNeo4j = true
MERGE (s)-[:HAS_TAG]->(t)
