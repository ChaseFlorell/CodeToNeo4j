namespace CodeToNeo4j.Graph;

public record Dependency(
    string Key,
    string Name,
    string Version
);
