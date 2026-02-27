using Neo4j.Driver;
using CodeToNeo4j.Console.Cypher;

namespace CodeToNeo4j.Console;

public interface INeo4jService : IAsyncDisposable
{
    Task VerifyNeo4JVersionAsync();
    Task EnsureSchemaAsync(string databaseName);
    Task UpsertProjectAsync(string repoKey, string databaseName);
    Task UpsertFileAsync(string fileKey, string filePath, string fileHash, string repoKey, string databaseName);
    Task DeletePriorSymbolsAsync(string fileKey, string databaseName);
    Task FlushAsync(string repoKey, string? fileKey, List<SymbolRecord> symbols, List<RelRecord> rels, string databaseName);
}

public class Neo4jService(IDriver driver, CypherService cypherService) : INeo4jService
{
    public async Task VerifyNeo4JVersionAsync()
    {
        await using var session = driver.AsyncSession();
        var result = await session.ExecuteReadAsync(async tx =>
        {
            var cursor = await tx.RunAsync(cypherService.GetCypher(Queries.GetNeo4jVersion));
            return await cursor.SingleAsync();
        });

        var versionString = result["version"].As<string>();
        if (string.IsNullOrWhiteSpace(versionString))
        {
            throw new NotSupportedException("Could not determine Neo4j version.");
        }

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
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        var schema = cypherService.GetCypher(Queries.Schema);
        var statements = schema.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var cypher in statements)
        {
            await session.RunAsync(cypher);
        }
    }

    public async Task UpsertProjectAsync(string repoKey, string databaseName)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(cypherService.GetCypher(Queries.UpsertProject), new { key = repoKey, name = repoKey });
        });
    }

    public async Task UpsertFileAsync(string fileKey, string filePath, string fileHash, string repoKey, string databaseName)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(cypherService.GetCypher(Queries.UpsertFile), new { fileKey, path = filePath, hash = fileHash, repoKey });
        });
    }

    public async Task DeletePriorSymbolsAsync(string fileKey, string databaseName)
    {
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(cypherService.GetCypher(Queries.DeletePriorSymbols), new { fileKey });
        });
    }

    public async Task FlushAsync(string repoKey, string? fileKey, List<SymbolRecord> symbols, List<RelRecord> rels, string databaseName)
    {
        if (symbols.Count == 0 && rels.Count == 0) return;

        var symbolBatch = symbols.ToArray();
        var relBatch = rels.ToArray();
        symbols.Clear();
        rels.Clear();

        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            await tx.RunAsync(cypherService.GetCypher(Queries.UpsertSymbols), new { symbols = symbolBatch });
            await tx.RunAsync(cypherService.GetCypher(Queries.MergeRelationships), new { rels = relBatch });
        });
    }

    public async ValueTask DisposeAsync()
    {
        await driver.DisposeAsync();
    }
}
