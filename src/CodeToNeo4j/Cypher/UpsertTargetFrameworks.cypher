// Set target_frameworks as a flat property array on File and Symbol nodes.

// 1. Set target_frameworks on File nodes
UNWIND $items AS item
MATCH (f:File {key: item.fileKey})
SET f.target_frameworks = item.tfms
WITH COUNT(*) AS _ignore

// 2. Set target_frameworks on Symbol nodes
UNWIND $symbolItems AS si
MATCH (s:Symbol {key: si.symbolKey})
SET s.target_frameworks = si.tfms
