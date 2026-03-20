// Set targetFrameworks as a flat property array on File and Symbol nodes.

// 1. Set targetFrameworks on File nodes
UNWIND $items AS item
MATCH (f:File {key: item.fileKey})
SET f.targetFrameworks = item.tfms
WITH COUNT(*) AS _ignore

// 2. Set targetFrameworks on Symbol nodes
UNWIND $symbolItems AS si
MATCH (s:Symbol {key: si.symbolKey})
SET s.targetFrameworks = si.tfms
