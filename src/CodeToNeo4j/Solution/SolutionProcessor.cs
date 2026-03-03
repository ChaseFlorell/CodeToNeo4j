using System.IO.Abstractions;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Progress;
using CodeToNeo4j.VersionControl;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Solution;

public class SolutionProcessor(
    IVersionControlService versionControlService,
    IGraphService graphService,
    IFileService fileService,
    IFileSystem fileSystem,
    IProgressService progressService,
    IEnumerable<IDocumentHandler> handlers,
    IDependencyIngestor dependencyIngestor,
    ISolutionFileDiscoveryService discoveryService,
    ILogger<SolutionProcessor> logger) : ISolutionProcessor
{
    public async ValueTask ProcessSolution(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool skipDependencies, Accessibility minAccessibility, IEnumerable<string> includeExtensions)
    {
        var extensionsToInclude = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var solutionRoot = sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory();
        logger.LogInformation("Processing solution: {SlnPath}", sln.FullName);
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message));

        logger.LogInformation("Opening solution...");
        var solution = await workspace.OpenSolutionAsync(sln.FullName).ConfigureAwait(false);
        logger.LogInformation("Solution opened successfully.");

        if (!skipDependencies)
        {
            await dependencyIngestor.IngestDependencies(solution, repoKey, databaseName).ConfigureAwait(false);
        }

        var diffResult = await GetChangedFiles(sln, diffBase, extensionsToInclude).ConfigureAwait(false);

        if (diffResult?.DeletedFiles.Count > 0)
        {
            logger.LogInformation("Marking {Count} files as deleted that were removed in git...", diffResult.DeletedFiles.Count);
            foreach (var deletedFile in diffResult.DeletedFiles)
            {
                var fileKey = $"{repoKey}:{fileService.GetRelativePath(solutionRoot, deletedFile)}";
                await graphService.MarkFileAsDeleted(fileKey, databaseName).ConfigureAwait(false);
            }
        }

        if (diffResult?.Commits.Any() ?? false)
        {
            logger.LogInformation("Ingesting {Count} commits since {DiffBase}...", diffResult.Commits.Count(), diffBase);
            await graphService.UpsertCommits(repoKey, solutionRoot, diffResult.Commits, databaseName).ConfigureAwait(false);
        }

        var discoveredFiles = await discoveryService.GetFilesToProcess(sln, solution, extensionsToInclude).ConfigureAwait(false);
        var filesToProcess = FilterFiles(discoveredFiles, diffResult?.ModifiedFiles);

        var totalFiles = filesToProcess.Length;
        var symbolBuffer = new List<Symbol>(batchSize);
        var relBuffer = new List<Relationship>(batchSize);

        for (var i = 0; i < totalFiles; i += batchSize)
        {
            var chunk = filesToProcess.Skip(i).Take(batchSize).ToArray();
            var tasks = chunk.Select((file, index) => ProcessFile(file, solutionRoot, repoKey, databaseName, i + index + 1, totalFiles, minAccessibility)).ToArray();

            var results = await Task.WhenAll(tasks.Select(t => t.AsTask())).ConfigureAwait(false);

            foreach (var result in results)
            {
                symbolBuffer.AddRange(result.Symbols);
                relBuffer.AddRange(result.Relationships);
            }

            if (symbolBuffer.Count > 0 || relBuffer.Count > 0)
            {
                logger.LogDebug("Flushing {SymbolCount} symbols and {RelCount} relationships to Neo4j...", symbolBuffer.Count, relBuffer.Count);
                await graphService.Flush(symbolBuffer, relBuffer, databaseName).ConfigureAwait(false);
                symbolBuffer.Clear();
                relBuffer.Clear();
            }
        }

        foreach (var handler in handlers.Where(h => h.NumberOfFilesHandled > 0))
        {
            logger.LogInformation("{FileExtension} files handled: {Count}", handler.FileExtension, handler.NumberOfFilesHandled);
        }

        logger.LogInformation("Done.");
    }

    private ProcessedFile[] FilterFiles(IEnumerable<ProcessedFile> discoveredFiles, HashSet<string>? changedFiles)
    {
        var result = discoveredFiles.ToArray();

        switch (changedFiles?.Any() ?? false)
        {
            case true:
                result = result
                    .Where(f => changedFiles.Contains(f.FilePath))
                    .ToArray();
                break;
            default:
            {
                if (changedFiles is not null)
                {
                    // If changedFiles is an empty collection (not null), it means we ARE in incremental mode
                    // but no files matched. So we return empty.
                    result = [];
                }

                break;
            }
        }

        return result;
    }

    private async ValueTask<DiffResult?> GetChangedFiles(FileInfo sln, string? diffBase, HashSet<string> includeExtensions)
    {
        if (diffBase is null) return null;

        var result = await versionControlService.GetChangedFiles(diffBase, sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory(), includeExtensions).ConfigureAwait(false);

        logger.LogInformation("Incremental indexing enabled. Found {ModifiedCount} modified and {DeletedCount} deleted files since {DiffBase}", result.ModifiedFiles.Count, result.DeletedFiles.Count, diffBase);

        return result;
    }

    private async ValueTask<(List<Symbol> Symbols, List<Relationship> Relationships)> ProcessFile(
        ProcessedFile file,
        string solutionRoot,
        string repoKey,
        string databaseName,
        int currentFileIndex,
        int totalFiles,
        Accessibility minAccessibility)
    {
        var filePath = file.FilePath;
        var relativePath = fileSystem.Path.GetRelativePath(solutionRoot, filePath).Replace('\\', '/');

        progressService.ReportProgress(currentFileIndex, totalFiles, relativePath);
        logger.LogDebug("Processing file: {FilePath}", filePath);

        var fileKey = $"{repoKey}:{filePath}";
        var fileHash = fileService.ComputeSha256(await fileSystem.File.ReadAllBytesAsync(filePath).ConfigureAwait(false));
        var metadata = await versionControlService.GetFileMetadata(filePath, solutionRoot).ConfigureAwait(false);

        await graphService.UpsertFile(fileKey, filePath, fileHash, metadata, repoKey, databaseName).ConfigureAwait(false);
        await graphService.DeletePriorSymbols(fileKey, databaseName).ConfigureAwait(false);

        var symbols = new List<Symbol>();
        var relationships = new List<Relationship>();

        var handler = handlers.FirstOrDefault(h => h.CanHandle(filePath));
        if (handler != null)
        {
            await handler.HandleAsync(file.Document, file.Compilation, repoKey, fileKey, filePath, symbols, relationships, databaseName, minAccessibility).ConfigureAwait(false);
        }
        else
        {
            logger.LogDebug("No handler found for file: {FilePath}", filePath);
        }

        logger.LogDebug("Indexed {FilePath}", filePath);
        return (symbols, relationships);
    }
}