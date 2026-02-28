using CodeToNeo4j.VersionControl;

namespace CodeToNeo4j.Neo4j;

public interface INeo4jService : IAsyncDisposable
{
    ValueTask VerifyNeo4jVersion();
    ValueTask EnsureSchema(string databaseName);
    ValueTask UpsertProject(string repoKey, string databaseName);
    ValueTask UpsertFile(string fileKey, string filePath, string fileHash, FileMetadata metadata, string repoKey, string databaseName);
    ValueTask DeletePriorSymbols(string fileKey, string databaseName);
    ValueTask MarkFileAsDeleted(string fileKey, string databaseName);
    ValueTask UpsertDependencies(string repoKey, IEnumerable<DependencyRecord> dependencies, string databaseName);
    ValueTask Flush(string repoKey, string? fileKey, IEnumerable<SymbolRecord> symbols, IEnumerable<RelRecord> rels, string databaseName);
}