namespace CodeToNeo4j.Graph;

public record FileResult(string? Namespace, string? FileKey, IReadOnlyCollection<UrlNode>? UrlNodes = null);
