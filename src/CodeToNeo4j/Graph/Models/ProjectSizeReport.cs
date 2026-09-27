namespace CodeToNeo4j.Graph.Models;

public sealed record ProjectSizeReport(
	string RepoKey,
	string? Name,
	long FileCount,
	long SymbolCount,
	long DependencyCount,
	long CommitCount,
	long AuthorCount,
	long TotalCount);
