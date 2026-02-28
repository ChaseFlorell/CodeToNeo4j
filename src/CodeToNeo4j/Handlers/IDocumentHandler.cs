using Microsoft.CodeAnalysis;
using CodeToNeo4j.Neo4j;

namespace CodeToNeo4j.Handlers;

public interface IDocumentHandler
{
    bool CanHandle(string filePath);
    ValueTask HandleAsync(
        Document document,
        Compilation compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
        string databaseName);
}
