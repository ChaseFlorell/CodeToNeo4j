namespace CodeToNeo4j;

public record SymbolRecord(
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