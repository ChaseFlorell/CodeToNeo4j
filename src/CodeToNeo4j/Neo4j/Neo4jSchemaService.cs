using System.Diagnostics.CodeAnalysis;
using CodeToNeo4j.Cypher;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;

namespace CodeToNeo4j.Neo4j;

[ExcludeFromCodeCoverage(Justification = "Requires a live Neo4j database connection")]
public class Neo4jSchemaService(
	IDriver driver,
	ICypherService cypherService,
	ILogger<Neo4jSchemaService> logger) : INeo4jSchemaService
{
	public async Task Initialize(string? repoKey, string databaseName)
	{
		logger.LogInformation("Initializing Neo4j driver...");

		await VerifyNeo4jVersion().ConfigureAwait(false);
		logger.LogInformation("Neo4j version verified.");

		await EnsureSchema(databaseName).ConfigureAwait(false);
		logger.LogInformation("Neo4j schema ensured for database: {DatabaseName}", databaseName);

		if (repoKey is not null)
		{
			await UpsertProject(repoKey, databaseName).ConfigureAwait(false);
			logger.LogInformation("Project upserted: {RepositoryKey}", repoKey);
		}
	}

	private async Task VerifyNeo4jVersion()
	{
		logger.LogDebug("Verifying Neo4j version...");
		await using var session = driver.AsyncSession();
		var result = await session.ExecuteReadAsync(async tx =>
		{
			var cursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.GetNeo4jVersion)).ConfigureAwait(false);
			return await cursor.SingleAsync().ConfigureAwait(false);
		}).ConfigureAwait(false);

		var versionString = result["version"].As<string>();
		if (string.IsNullOrWhiteSpace(versionString))
		{
			throw new NotSupportedException("Could not determine Neo4j version.");
		}

		logger.LogDebug("Detected Neo4j version: {VersionString}", versionString);

		var versionSpan = versionString.AsSpan();
		var hyphenIndex = versionSpan.IndexOf('-');
		var versionPart = hyphenIndex == -1 ? versionSpan : versionSpan[..hyphenIndex];

		if (Version.TryParse(versionPart, out var version))
		{
			if (version.Major < 5)
			{
				throw new NotSupportedException($"Neo4j version {versionString} is not supported. Minimum required version is 5.0.");
			}
		}
		else
		{
			if (versionSpan.Length > 0 && char.IsDigit(versionSpan[0]) && int.TryParse(versionSpan[..1], out var major) && major < 5)
			{
				throw new NotSupportedException($"Neo4j version {versionString} is not supported. Minimum required version is 5.0.");
			}
		}
	}

	private async Task EnsureSchema(string databaseName)
	{
		if (databaseName.Any(char.IsUpper))
		{
			logger.LogWarning(
				"Database name '{DatabaseName}' contains uppercase letters. Neo4j 5.0+ usually requires lowercase database names. This may cause connection issues or use the wrong database.",
				databaseName);
		}

		logger.LogDebug("Ensuring schema for database: {DatabaseName}", databaseName);
		var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		var schema = cypherService.GetCypher(Queries.Schema);
		var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

		await Task.WhenAll(statements.Select(cypher => session.RunWithRetry(cypher)))
			.ContinueWith(async _ => await session.DisposeAsync());
	}

	private async Task UpsertProject(string repoKey, string databaseName)
	{
		logger.LogDebug("Upserting project: {RepoKey} in database: {DatabaseName}", repoKey, databaseName);
		await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
		await session.ExecuteWriteAsync(async tx =>
				await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertProject), new { key = repoKey, name = repoKey }))
			.ConfigureAwait(false);
	}
}
