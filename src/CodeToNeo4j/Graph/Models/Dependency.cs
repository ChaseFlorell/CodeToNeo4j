namespace CodeToNeo4j.Graph.Models;

public record Dependency(
	string Key,
	string Name,
	string Version
);
