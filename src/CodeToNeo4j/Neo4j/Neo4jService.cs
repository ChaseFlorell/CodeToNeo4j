using CodeToNeo4j.Cypher;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Graph;
using CodeToNeo4j.VersionControl;
using Neo4j.Driver;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Neo4j;

public class Neo4jService(
    IDriver driver,
    ICypherService cypherService,
    IFileService fileService,
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

    public async Task MarkFileAsDeleted(string filePath, string databaseName)
    {
        logger.LogDebug("Marking file and its symbols as deleted for filePath: {FilePath} in database: {DatabaseName}", filePath, databaseName);
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx => { await tx.RunWithRetry(cypherService.GetCypher(Queries.MarkFileAsDeleted), new { filePath }).ConfigureAwait(false); }).ConfigureAwait(false);
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
                return new Dictionary<string, object?>
                {
                    ["key"] = key,
                    ["path"] = relativePath,
                    ["namespace"] = ns,
                    ["deleted"] = f.IsDeleted
                };
            }).ToArray()
        }).ToArray();

        if (commitBatch.Length == 0) return;

        logger.LogDebug("Upserting {Count} commits for {RepositoryKey} in database: {DatabaseName}", commitBatch.Length, repoKey, databaseName);
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx => { await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertCommit), new { commits = commitBatch }).ConfigureAwait(false); }).ConfigureAwait(false);
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

        if (depBatch.Length == 0) return;

        logger.LogDebug("Upserting {Count} dependencies for {RepositoryKey} in database: {DatabaseName}", depBatch.Length, repoKey, databaseName);
        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx => await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertDependencies), new { dependencies = depBatch }))
            .ConfigureAwait(false);
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
            ["repoKey"] = file.RepoKey
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
            ["documentation"] = s.Documentation,
            ["comments"] = s.Comments,
            ["version"] = s.Version
        }).ToArray();

        var relBatch = relationships.Select(r => new Dictionary<string, object?>
        {
            ["fromKey"] = r.FromKey,
            ["toKey"] = r.ToKey,
            ["relType"] = r.RelType
        }).ToArray();

        var tagBatch = symbols
            .Where(s => !string.IsNullOrWhiteSpace(s.Namespace))
            .Select(s => new Dictionary<string, object?>
            {
                ["symbolKey"] = s.Key,
                ["tags"] = NamespaceTagParser.ParseTags(s.Namespace).ToArray()
            })
            .Where(x => ((string[])x["tags"]!).Length > 0)
            .ToArray();

        if (symbolBatch.Length == 0 && relBatch.Length == 0) return;

        logger.LogDebug("Flushing {SymbolCount} symbols and {RelCount} relationships to Neo4j (Database: {DatabaseName})...", symbolBatch.Length, relBatch.Length, databaseName);

        await using var session = driver.AsyncSession(o => o.WithDatabase(databaseName));
        await session.ExecuteWriteAsync(async tx =>
        {
            var tasks = new List<Task>();

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
                var cursor = await tx.RunWithRetry(cypherService.GetCypher(Queries.PurgeData), new { repoKey, extensions, purgeDependencies, batchSize })
                    .ConfigureAwait(false);
                var record = await cursor.SingleAsync().ConfigureAwait(false);
                return record[0].As<long>();
            }).ConfigureAwait(false);

            if (deletedInBatch == 0) break;
            totalDeleted += deletedInBatch;
            logger.LogDebug("Purged {BatchCount} items... (Total: {TotalDeleted})", deletedInBatch, totalDeleted);
        }

        logger.LogInformation("Purge complete for {PurgeTarget}. Total items deleted: {TotalDeleted}", purgeTarget, totalDeleted);
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
            logger.LogWarning("Database name '{DatabaseName}' contains uppercase letters. Neo4j 5.0+ usually requires lowercase database names. This may cause connection issues or use the wrong database.", databaseName);
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
        await session.ExecuteWriteAsync(async tx => await tx.RunWithRetry(cypherService.GetCypher(Queries.UpsertProject), new { key = repoKey, name = repoKey }))
            .ConfigureAwait(false);
    }
}