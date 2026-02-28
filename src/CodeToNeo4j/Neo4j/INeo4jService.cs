namespace CodeToNeo4j.Neo4j;

public interface INeo4jService : IAsyncDisposable
{
    Task VerifyNeo4JVersionAsync();
    Task EnsureSchemaAsync(string databaseName);
    Task UpsertProjectAsync(string repoKey, string databaseName);
    Task UpsertFileAsync(string fileKey, string filePath, string fileHash, string repoKey, string databaseName);
    Task DeletePriorSymbolsAsync(string fileKey, string databaseName);
    Task UpsertDependenciesAsync(string repoKey, List<DependencyRecord> dependencies, string databaseName);
    Task FlushAsync(string repoKey, string? fileKey, List<SymbolRecord> symbols, List<RelRecord> rels, string databaseName);
}