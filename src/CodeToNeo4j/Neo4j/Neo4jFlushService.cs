using CodeToNeo4j.Cypher;
using CodeToNeo4j.Extensions;
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

	// Caps the row count of any single Cypher UNWIND, independent of the caller's --batch-size.
	// A single file can produce a burst of symbols/relationships far larger than --batch-size
	// (e.g. a generated file with thousands of cross-references); without this cap that one
	// file's flush becomes a single oversized transaction regardless of upstream buffering.
	internal const int MaxRowsPerQuery = 250;

	private static IEnumerable<Dictionary<string, object?>[]> Chunk(Dictionary<string, object?>[] source, int chunkSize)
	{
		for (var i = 0; i < source.Length; i += chunkSize)
		{
			yield return source[i..Math.Min(i + chunkSize, source.Length)];
		}
	}

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

		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));

		// Each chunk commits as its own transaction so Neo4j releases that transaction's memory
		// before the next chunk starts -- chunking rows within one transaction (as before) doesn't
		// bound peak transaction memory, since Neo4j tracks write/undo state for the whole
		// transaction until commit, not per-statement.
		foreach (var chunk in Chunk(fileBatch, MaxRowsPerQuery))
		{
			await session.ExecuteWriteAsync(async tx =>
			{
				var filesCursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertFile), new { files = chunk }).ConfigureAwait(false);
				await filesCursor.ConsumeAsync().ConfigureAwait(false);
			}).ConfigureAwait(false);
		}
	}

	public async Task FlushSymbols(IEnumerable<string> fileKeys, IEnumerable<Symbol> symbols, IEnumerable<Relationship> relationships, string databaseName)
	{
		var fileKeyArray = fileKeys.ToArray();
		var symbolArray = symbols.ToArray();
		var symbolBatch = symbolArray.Select(s => new Dictionary<string, object?>
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
			["documentation"] = s.Documentation.Truncate(),
			["comments"] = s.Comments.Truncate(),
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

		var tagBatch = symbolArray
			.Where(s => !string.IsNullOrWhiteSpace(s.Namespace))
			.Select(s => new Dictionary<string, object?> { ["symbolKey"] = s.Key, ["tags"] = namespaceTagParser.ParseTags(s.Namespace).ToArray() })
			.Where(x => ((string[])x["tags"]!).Length > 0)
			.ToArray();

		if (symbolBatch.Length == 0 && relBatch.Length == 0 && fileKeyArray.Length == 0)
		{
			return;
		}

		if (symbolBatch.Length > 0 || relBatch.Length > 0)
		{
			logger.LogDebug("Flushing {SymbolCount} symbols and {RelCount} relationships to Neo4j (Database: {DatabaseName})...", symbolBatch.Length,
				relBatch.Length, databaseName);

			await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));

			foreach (var chunk in Chunk(symbolBatch, MaxRowsPerQuery))
			{
				await session.ExecuteWriteAsync(async tx =>
				{
					var symbolsCursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertSymbols), new { symbols = chunk }).ConfigureAwait(false);
					await symbolsCursor.ConsumeAsync().ConfigureAwait(false);
				}).ConfigureAwait(false);
			}

			foreach (var chunk in Chunk(relBatch, MaxRowsPerQuery))
			{
				await session.ExecuteWriteAsync(async tx =>
				{
					var relsCursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.MergeRelationships), new { rels = chunk }).ConfigureAwait(false);
					await relsCursor.ConsumeAsync().ConfigureAwait(false);
				}).ConfigureAwait(false);
			}

			if (tagBatch.Length > 0)
			{
				logger.LogDebug("Upserting namespace tags for {Count} symbols (Database: {DatabaseName})...", tagBatch.Length, databaseName);
				await using var tagSession = driver.AsyncSession(o => o.WithDatabase(databaseName));
				foreach (var chunk in Chunk(tagBatch, MaxRowsPerQuery))
				{
					await tagSession.ExecuteWriteAsync(async tx =>
					{
						var tagsCursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertTags), new { symbolTags = chunk }).ConfigureAwait(false);
						await tagsCursor.ConsumeAsync().ConfigureAwait(false);
					}).ConfigureAwait(false);
				}
			}
		}

		if (fileKeyArray.Length > 0)
		{
			// Runs only after the replacement symbols/relationships above are fully committed, so a
			// mid-flush failure leaves stale symbols behind instead of deleting data before its
			// replacement exists. Only symbols absent from this batch's key set are removed, so
			// symbols untouched by this flush (e.g. a different file's) are never at risk.
			// Chunked by file key, same as every other write in this file, so a sweep spanning many
			// files' worth of existing symbols can't build one unbounded transaction either.
			var keepKeys = symbolArray.Select(s => s.Key).ToArray();
			logger.LogDebug("Sweeping stale symbols for {Count} files (Database: {DatabaseName})...", fileKeyArray.Length, databaseName);
			await using var sweepSession = driver.AsyncSession(o => o.WithDatabase(databaseName));
			foreach (var chunk in fileKeyArray.Chunk(MaxRowsPerQuery))
			{
				await sweepSession.ExecuteWriteAsync(async tx =>
				{
					var sweepCursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.DeletePriorSymbols), new { fileKeys = chunk, keepKeys })
						.ConfigureAwait(false);
					await sweepCursor.ConsumeAsync().ConfigureAwait(false);
				}).ConfigureAwait(false);
			}
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
		foreach (var chunk in Chunk(urlBatch, MaxRowsPerQuery))
		{
			await session.ExecuteWriteAsync(async tx =>
			{
				var cursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertDependencyUrls), new { urls = chunk });
				await cursor.ConsumeAsync();
			}).ConfigureAwait(false);
		}
	}
}
