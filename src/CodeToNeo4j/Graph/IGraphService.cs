using CodeToNeo4j.VersionControl;

namespace CodeToNeo4j.Graph;

public interface IGraphService
{
    Task Initialize(string? repoKey, string databaseName);
    Task MarkFileAsDeleted(string fileKey, string databaseName);
    Task UpsertCommits(string? repoKey, string solutionRoot, IEnumerable<CommitMetadata> commits, string databaseName);
    Task UpsertDependencies(string? repoKey, IEnumerable<Dependency> dependencies, string databaseName);
    Task FlushFiles(IEnumerable<FileMetaData> files, string databaseName);
    Task FlushSymbols(IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName);
    Task PurgeData(string? repoKey, IEnumerable<string>? includeExtensions, string databaseName);
}