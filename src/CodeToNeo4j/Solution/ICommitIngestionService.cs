namespace CodeToNeo4j.Solution;

public interface ICommitIngestionService
{
    Task IngestCommits(string diffBase, string solutionRoot, string? repoKey, string databaseName, int batchSize);
}