using CodeToNeo4j.Cypher;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Graph;
using CodeToNeo4j.VersionControl;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace CodeToNeo4j.Neo4j;

public class Neo4jService(
	IDriver driver,
	ICypherService cypherService,
	IFileService fileService,
	INeo4jSchemaService schemaService,
	INeo4jFlushService flushService,
	ILogger<Neo4jService> logger)
	: IGraphService, IAsyncDisposable, IDisposable
{
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);

		Dispose(false);
		GC.SuppressFinalize(this);
	}

	public Task Initialize(string? repoKey, string databaseName) =>
		schemaService.Initialize(repoKey, databaseName);

	public async Task MarkFileAsDeleted(string filePath, string databaseName)
	{
		logger.LogDebug("Marking file and its symbols as deleted for filePath: {FilePath} in database: {DatabaseName}", filePath, databaseName);
		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		await session.ExecuteWriteAsync(async tx =>
		{
			await tx.RunWithRetry(cypherService.GetCypher(Queries.MarkFileAsDeleted), new { filePath }).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	public async Task UpsertCommits(string? repoKey, string solutionRoot, IEnumerable<CommitMetadata> commits, string databaseName)
	{
		var commitBatch = commits.Select(c => new Dictionary<string, object?>
		{
			["hash"] = c.Hash,
			["authorName"] = c.AuthorName,
			["authorEmail"] = c.AuthorEmail,
			["date"] = c.Date.ToString("O"),
			["message"] = c.Message,
			["repoKey"] = repoKey,
			["changedFiles"] = c.ChangedFiles.Select(f =>
			{
				var relativePath = fileService.GetRelativePath(solutionRoot, f.Path);
				var (key, ns) = fileService.InferFileMetadata(relativePath);
				return new Dictionary<string, object?> { ["key"] = key, ["path"] = relativePath, ["namespace"] = ns, ["deleted"] = f.IsDeleted };
			}).ToArray()
		}).ToArray();

		if (commitBatch.Length == 0)
		{
			return;
		}

		logger.LogDebug("Upserting {Count} commits for {RepositoryKey} in database: {DatabaseName}", commitBatch.Length, repoKey, databaseName);
		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		await session.ExecuteWriteAsync(async tx =>
		{
			await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertCommit), new { commits = commitBatch }).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	public async Task UpsertDependencies(string? repoKey, IEnumerable<Dependency> dependencies, string databaseName)
	{
		var depBatch = dependencies.Select(d => new Dictionary<string, object?>
		{
			["key"] = d.Key,
			["name"] = d.Name,
			["version"] = d.Version,
			["repoKey"] = repoKey
		}).ToArray();

		if (depBatch.Length == 0)
		{
			return;
		}

		logger.LogDebug("Upserting {Count} dependencies for {RepositoryKey} in database: {DatabaseName}", depBatch.Length, repoKey, databaseName);
		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		await session.ExecuteWriteAsync(async tx =>
				await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertDependencies), new { dependencies = depBatch }))
			.ConfigureAwait(false);
	}

	public Task FlushFiles(IEnumerable<FileMetaData> files, string databaseName) =>
		flushService.FlushFiles(files, databaseName);

	public Task FlushSymbols(IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName) =>
		flushService.FlushSymbols(symbols, relationships, databaseName);

	public Task UpsertDependencyUrls(IEnumerable<UrlNode> urlNodes, string databaseName) =>
		flushService.UpsertDependencyUrls(urlNodes, databaseName);

	public Task FlushTargetFrameworks(IEnumerable<TargetFrameworkBatch> batches, string databaseName) =>
		flushService.FlushTargetFrameworks(batches, databaseName);

	public async Task PurgeData(string? repoKey, IEnumerable<string>? includeExtensions, string databaseName, bool purgeDependencies, int batchSize)
	{
		var purgeTarget = repoKey is null ? "ALL CodeToNeo4j data" : $"repoKey '{repoKey}'";
		logger.LogInformation("Purging data for {PurgeTarget} (Database: {DatabaseName})...", purgeTarget, databaseName);
		var extensions = includeExtensions?.ToArray();

		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		var totalDeleted = 0L;

		while (true)
		{
			var deletedInBatch = await session.ExecuteWriteAsync(async tx =>
			{
				var cursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.PurgeData),
						new { repoKey, extensions, purgeDependencies, batchSize })
					.ConfigureAwait(false);
				var record = await cursor.SingleAsync().ConfigureAwait(false);
				return record[0].As<long>();
			}).ConfigureAwait(false);

			if (deletedInBatch == 0)
			{
				break;
			}

			totalDeleted += deletedInBatch;
			logger.LogDebug("Purged {BatchCount} items... (Total: {TotalDeleted})", deletedInBatch, totalDeleted);
		}

		logger.LogInformation("Purge complete for {PurgeTarget}. Total items deleted: {TotalDeleted}", purgeTarget, totalDeleted);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			driver.Dispose();
		}
	}

	protected virtual async ValueTask DisposeAsyncCore()
	{
		logger.LogDebug("Disposing Neo4j driver...");
		await driver.DisposeAsync().ConfigureAwait(false);
	}

	~Neo4jService()
	{
		Dispose(false);
	}
}
