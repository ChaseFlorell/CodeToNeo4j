using CodeToNeo4j.Console.Cypher;
using Neo4j.Driver;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Console.Neo4j;

public class Neo4jService(IDriver driver, ICypherService cypherService, ILogger<Neo4jService> logger) : INeo4jService
{
    public async Task VerifyNeo4JVersionAsync()
    {
        logger.LogDebug("Verifying Neo4j version...");
        await using var session = driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsyncWithRetry(cypherService.GetCypher(Queries.GetNeo4jVersion));
            return await cursor.SingleAsync();
        });

        var versionString = result["version"].As<string>();
        if (string.IsNullOrWhiteSpace(versionString))
        {
            throw new NotSupportedException("Could not determine Neo4j version.");
        }

        logger.LogDebug("Detected Neo4j version: {VersionString}", versionString);

        if (Version.TryParse(versionString.Split('-')[0], out var version))
        {
            if (version.Major < 5)
            {
                throw new NotSupportedException($"Neo4j version {versionString} is not supported. Minimum required version is 5.0.");
            }
        }
        else
        {
            if (char.IsDigit(versionString[0]) && int.TryParse(versionString[0].ToString(), out var major) && major < 5)
            {
                throw new NotSupportedException($"Neo4j version {versionString} is not supported. Minimum required version is 5.0.");
            }
        }
    }

    public async Task EnsureSchemaAsync(string databaseName)
    {
        logger.LogDebug("Ensuring schema for database: {DatabaseName}", databaseName);
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        var schema = cypherService.GetCypher(Queries.Schema);
        var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var cypher in statements)
        {
            await session.RunAsyncWithRetry(cypher);
        }
    }

    public async Task UpsertProjectAsync(string repoKey, string databaseName)
    {
        logger.LogDebug("Upserting project: {RepoKey} in database: {DatabaseName}", repoKey, databaseName);
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsyncWithRetry(cypherService.GetCypher(Queries.UpsertProject), new { key = repoKey, name = repoKey });
        });
    }

    public async Task UpsertFileAsync(string fileKey, string filePath, string fileHash, string repoKey, string databaseName)
    {
        logger.LogDebug("Upserting file: {FilePath} in database: {DatabaseName}", filePath, databaseName);
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsyncWithRetry(cypherService.GetCypher(Queries.UpsertFile), new { fileKey, path = filePath, hash = fileHash, repoKey });
        });
    }

    public async Task DeletePriorSymbolsAsync(string fileKey, string databaseName)
    {
        logger.LogDebug("Deleting prior symbols for fileKey: {FileKey} in database: {DatabaseName}", fileKey, databaseName);
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsyncWithRetry(cypherService.GetCypher(Queries.DeletePriorSymbols), new { fileKey });
        });
    }

    public async Task FlushAsync(string repoKey, string? fileKey, List<SymbolRecord> symbols, List<RelRecord> rels, string databaseName)
    {
        if (symbols.Count == 0 && rels.Count == 0) return;

        logger.LogDebug("Flushing {SymbolCount} symbols and {RelCount} relationships to Neo4j (Database: {DatabaseName})...", symbols.Count, rels.Count, databaseName);

        var symbolBatch = symbols.ToArray();
        var relBatch = rels.ToArray();
        symbols.Clear();
        rels.Clear();

        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsyncWithRetry(cypherService.GetCypher(Queries.UpsertSymbols), new { symbols = symbolBatch });
            await tx.RunAsyncWithRetry(cypherService.GetCypher(Queries.MergeRelationships), new { rels = relBatch });
        });
    }

    public async ValueTask DisposeAsync()
    {
        logger.LogDebug("Disposing Neo4j driver...");
        await driver.DisposeAsync();
    }
}
