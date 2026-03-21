namespace CodeToNeo4j.Graph.Models;

public record FileResult(string? Namespace, string? FileKey, IReadOnlyCollection<UrlNode>? UrlNodes = null);
