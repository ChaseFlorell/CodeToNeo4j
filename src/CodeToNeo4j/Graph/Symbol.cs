namespace CodeToNeo4j.Graph;

public record Symbol(
    string Key,
    string Name,
    string Kind,
    string Fqn,
    string Accessibility,
    string FileKey,
    string FilePath,
    int StartLine,
    int EndLine,
    string? Documentation,
    string? Comments
);