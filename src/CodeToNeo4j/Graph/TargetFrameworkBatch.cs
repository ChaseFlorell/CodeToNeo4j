namespace CodeToNeo4j.Graph;

public record TargetFrameworkBatch(
    string FileKey,
    IReadOnlyCollection<string> SymbolKeys,
    IReadOnlyCollection<string> TargetFrameworks);
