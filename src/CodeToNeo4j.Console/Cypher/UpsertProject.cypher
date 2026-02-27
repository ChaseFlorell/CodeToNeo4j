MERGE (p:Project {key:$key})
SET p.name=$name, p.updatedAt=datetime()
