namespace CodeToNeo4j.Graph.Models;

public record Symbol(
	string Key,
	string Name,
	string Kind,
	string Class,
	string Fqn,
	string Accessibility,
	string FileKey,
	string RelativePath,
	int StartLine,
	int EndLine,
	string? Documentation,
	string? Comments,
	string? Namespace,
	string? Version = null,
	string Language = "unknown",
	string Technology = "unknown"
);
