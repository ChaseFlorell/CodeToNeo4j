namespace CodeToNeo4j.Solution.Ingestion;

public interface IDependencyIngestor
{
	Task IngestDependencies(Microsoft.CodeAnalysis.Solution solution, string? repoKey, string databaseName);
}
