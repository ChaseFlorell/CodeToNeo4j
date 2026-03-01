namespace CodeToNeo4j.Solution;

public interface IDependencyIngestor
{
    ValueTask IngestDependencies(Microsoft.CodeAnalysis.Solution solution, string repoKey, string databaseName);
}
