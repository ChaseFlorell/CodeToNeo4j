using CodeToNeo4j.VersionControl;

namespace CodeToNeo4j.Graph;

public interface IGraphService : IAsyncDisposable
{
    Task Initialize(string repoKey, string databaseName);
    Task MarkFileAsDeleted(string fileKey, string databaseName);
    Task UpsertCommits(string repoKey, string solutionRoot, IEnumerable<CommitMetadata> commits, string databaseName);
    Task UpsertDependencies(string repoKey, IEnumerable<Dependency> dependencies, string databaseName);
    Task UpsertFile(FileMetaData file, string databaseName);
    Task FlushSymbols(IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName);
}