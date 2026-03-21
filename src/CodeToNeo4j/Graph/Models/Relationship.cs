namespace CodeToNeo4j.Graph.Models;

public record Relationship(
	string FromKey,
	string ToKey,
	string RelType);
