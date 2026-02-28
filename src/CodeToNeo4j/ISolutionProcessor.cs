namespace CodeToNeo4j;

public interface ISolutionProcessor
{
    Task ProcessSolutionAsync(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool force, bool skipDependencies);
}