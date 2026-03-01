using CodeToNeo4j.VersionControl;

namespace CodeToNeo4j.Graph;

public interface IGraphService : IAsyncDisposable
{
    ValueTask Initialize(string optionsRepoKey, string optionsDatabaseName);
    ValueTask UpsertFile(string fileKey, string filePath, string fileHash, FileMetadata metadata, string repoKey, string databaseName);
    ValueTask DeletePriorSymbols(string fileKey, string databaseName);
    ValueTask MarkFileAsDeleted(string fileKey, string databaseName);
    ValueTask UpsertCommits(string repoKey, string solutionRoot, IEnumerable<CommitMetadata> commits, string databaseName);
    ValueTask UpsertDependencies(string repoKey, IEnumerable<Dependency> dependencies, string databaseName);
    ValueTask Flush(IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName);
}