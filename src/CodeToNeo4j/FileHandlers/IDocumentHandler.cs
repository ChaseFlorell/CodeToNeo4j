using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public interface IDocumentHandler
{
    bool CanHandle(string filePath);
    ValueTask HandleAsync(
        Document? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
        string databaseName,
        Accessibility minAccessibility);

    int NumberOfFilesHandled { get; }
    string FileType { get; }
}
