using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Solution;

public record ProcessedFile(string FilePath, TextDocument? Document, Compilation? Compilation);
