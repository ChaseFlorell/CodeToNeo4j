using CodeToNeo4j.VersionControl;

namespace CodeToNeo4j.Graph;

public interface IGraphService : IAsyncDisposable
{
    ValueTask Initialize(string optionsRepoKey, string optionsDatabaseName);
    ValueTask MarkFileAsDeleted(string fileKey, string databaseName);
    ValueTask UpsertCommits(string repoKey, string solutionRoot, IEnumerable<CommitMetadata> commits, string databaseName);
    ValueTask UpsertDependencies(string repoKey, IEnumerable<Dependency> dependencies, string databaseName);
    ValueTask UpsertFile(FileMetaData file, IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName);
}