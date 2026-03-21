namespace CodeToNeo4j.Solution.Ingestion;

public interface ICommitIngestionService
{
	Task IngestCommits(string diffBase, string solutionRoot, string? repoKey, string databaseName, int batchSize);
}
