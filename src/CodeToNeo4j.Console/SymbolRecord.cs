namespace CodeToNeo4j.Console;

public record SymbolRecord(
    string Key,
    string Name,
    string Kind,
    string Fqn,
    string Accessibility,
    string FileKey,
    string FilePath,
    int StartLine,
    int EndLine
);