using CodeToNeo4j.Cypher;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Graph.Xml;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace CodeToNeo4j.Neo4j;

public class Neo4jFlushService(
	IDriver driver,
	ICypherService cypherService,
	INamespaceTagParser namespaceTagParser,
	ILogger<Neo4jFlushService> logger) : INeo4jFlushService
{
	internal const int MaxIndexedStringLength = 8000;

	internal static string? Truncate(string? value)
		=> value is { Length: > MaxIndexedStringLength }
			? value[..MaxIndexedStringLength]
			: value;

	public async Task FlushFiles(IEnumerable<FileMetaData> files, string databaseName)
	{
		var fileBatch = files.Select(file => new Dictionary<string, object?>
		{
			["fileKey"] = file.FileKey,
			["fileName"] = file.FileName,
			["path"] = file.RelativePath,
			["namespace"] = file.Namespace,
			["hash"] = file.FileHash,
			["created"] = file.Metadata.Created.ToString("O"),
			["lastModified"] = file.Metadata.LastModified.ToString("O"),
			["authors"] = file.Metadata.Authors.Select(a => new Dictionary<string, object?>
			{
				["name"] = a.Name,
				["firstCommit"] = a.FirstCommit.ToString("O"),
				["lastCommit"] = a.LastCommit.ToString("O"),
				["commitCount"] = a.CommitCount
			}).ToArray(),
			["commits"] = file.Metadata.Commits.ToArray(),
			["tags"] = file.Metadata.Tags.ToArray(),
			["repoKey"] = file.RepoKey,
			["language"] = file.Language,
			["technology"] = file.Technology,
		}).ToArray();

		if (fileBatch.Length == 0)
		{
			return;
		}

		logger.LogDebug("Flushing {Count} files to Neo4j (Database: {DatabaseName})...", fileBatch.Length, databaseName);

		var fileKeys = fileBatch.Select(f => f["fileKey"]).ToArray();

		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		await session.ExecuteWriteAsync(async tx =>
		{
			await tx.RunWithRetry(cypherService.GetCypher(Queries.DeletePriorSymbols), new { fileKeys }).ConfigureAwait(false);
			await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertFile), new { files = fileBatch }).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	public async Task FlushSymbols(IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName)
	{
		var symbolBatch = symbols.Select(s => new Dictionary<string, object?>
		{
			["key"] = s.Key,
			["name"] = s.Name,
			["kind"] = s.Kind,
			["class"] = s.Class,
			["fqn"] = s.Fqn,
			["accessibility"] = s.Accessibility,
			["fileKey"] = s.FileKey,
			["filePath"] = s.RelativePath,
			["namespace"] = s.Namespace,
			["startLine"] = s.StartLine,
			["endLine"] = s.EndLine,
			["documentation"] = Truncate(s.Documentation),
			["comments"] = Truncate(s.Comments),
			["version"] = s.Version,
			["language"] = s.Language,
			["technology"] = s.Technology
		}).ToArray();

		var relBatch = relationships.Select(r => new Dictionary<string, object?>
		{
			["fromKey"] = r.FromKey,
			["toKey"] = r.ToKey,
			["relType"] = r.RelType
		}).ToArray();

		var tagBatch = symbols
			.Where(s => !string.IsNullOrWhiteSpace(s.Namespace))
			.Select(s => new Dictionary<string, object?> { ["symbolKey"] = s.Key, ["tags"] = namespaceTagParser.ParseTags(s.Namespace).ToArray() })
			.Where(x => ((string[])x["tags"]!).Length > 0)
			.ToArray();

		if (symbolBatch.Length == 0 && relBatch.Length == 0)
		{
			return;
		}

		logger.LogDebug("Flushing {SymbolCount} symbols and {RelCount} relationships to Neo4j (Database: {DatabaseName})...", symbolBatch.Length,
			relBatch.Length, databaseName);

		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		await session.ExecuteWriteAsync(async tx =>
		{
			List<Task> tasks = [];

			if (symbolBatch.Length > 0)
			{
				tasks.Add(tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertSymbols), new { symbols = symbolBatch }));
			}

			if (relBatch.Length > 0)
			{
				tasks.Add(tx.RunWithRetry(cypherService.GetCypher(Queries.MergeRelationships), new { rels = relBatch }));
			}

			if (tasks.Count > 0)
			{
				await Task.WhenAll(tasks).ConfigureAwait(false);
			}
		}).ConfigureAwait(false);

		if (tagBatch.Length > 0)
		{
			logger.LogDebug("Upserting namespace tags for {Count} symbols (Database: {DatabaseName})...", tagBatch.Length, databaseName);
			await using var tagSession = driver.AsyncSession(o => o.WithDatabase(databaseName));
			await tagSession.ExecuteWriteAsync(async tx =>
			{
				await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertTags), new { symbolTags = tagBatch }).ConfigureAwait(false);
			}).ConfigureAwait(false);
		}
	}

	public async Task UpsertDependencyUrls(IEnumerable<UrlNode> urlNodes, string databaseName)
	{
		var urlBatch = urlNodes.Select(u => new Dictionary<string, object?> { ["depKey"] = u.DepKey, ["urlKey"] = u.UrlKey, ["name"] = u.Name })
			.ToArray();

		if (urlBatch.Length == 0)
		{
			return;
		}

		logger.LogDebug("Upserting {Count} dependency URL nodes in database: {DatabaseName}", urlBatch.Length, databaseName);
		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		await session.ExecuteWriteAsync(async tx =>
				await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertDependencyUrls), new { urls = urlBatch }))
			.ConfigureAwait(false);
	}

}
