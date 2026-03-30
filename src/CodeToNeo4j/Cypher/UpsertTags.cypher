UNWIND $symbolTags AS st
MATCH (s:src__Symbol {key: st.symbolKey})
UNWIND st.tags AS tagName
MERGE (t:src__Tag {name: tagName})
SET t.CodeToNeo4j = true
MERGE (s)-[:src__HAS_TAG]->(t)
