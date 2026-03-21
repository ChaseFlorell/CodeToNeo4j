using CodeToNeo4j.Graph;
using CodeToNeo4j.VersionControl;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Solution.Ingestion;

public class CommitIngestionService(
	IVersionControlService versionControlService,
	IGraphService graphService,
	ILogger<CommitIngestionService> logger) : ICommitIngestionService
{
	public async Task IngestCommits(string diffBase, string solutionRoot, string? repoKey, string databaseName, int batchSize)
	{
		var totalCommits = await versionControlService.GetCommitCount(diffBase, solutionRoot).ConfigureAwait(false);
		if (totalCommits <= 0)
		{
			return;
		}

		logger.LogInformation("Ingesting {Count} commits since {DiffBase} in parallel batches...", totalCommits, diffBase);
		ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) };
		var skipValues = Enumerable.Range(0, (totalCommits + batchSize - 1) / batchSize).Select(i => i * batchSize).ToArray();

		await Parallel.ForEachAsync(skipValues, parallelOptions, async (skip, _) =>
		{
			var commits = await versionControlService.GetCommitBatch(diffBase, solutionRoot, batchSize, skip).ConfigureAwait(false);
			await graphService.UpsertCommits(repoKey, solutionRoot, commits, databaseName).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}
}
