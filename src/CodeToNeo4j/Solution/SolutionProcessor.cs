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
    public async ValueTask ProcessSolution(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool force, bool skipDependencies, Accessibility minAccessibility, IEnumerable<string> includeExtensions)
    {
        var extensionsToInclude = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var solutionRoot = sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory();
        logger.LogInformation("Processing solution: {SlnPath}", sln.FullName);
        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message));

        logger.LogInformation("Opening solution...");
        var solution = await workspace.OpenSolutionAsync(sln.FullName);
        logger.LogInformation("Solution opened successfully.");

        if (!skipDependencies)
        {
            await dependencyIngestor.IngestDependencies(solution, repoKey, databaseName);
        }

        var diffResult = await GetChangedFiles(sln, diffBase, force, extensionsToInclude);

        if (diffResult?.DeletedFiles.Count > 0)
        {
            logger.LogInformation("Marking {Count} files as deleted that were removed in git...", diffResult.DeletedFiles.Count);
            foreach (var deletedFile in diffResult.DeletedFiles)
            {
                var fileKey = $"{repoKey}:{fileService.GetRelativePath(solutionRoot, deletedFile)}";
                await graphService.MarkFileAsDeleted(fileKey, databaseName);
            }
        }

        if (diffResult is not null && diffResult.Commits.Any())
        {
            logger.LogInformation("Ingesting {Count} commits since {DiffBase}...", diffResult.Commits.Count(), diffBase);
            await graphService.UpsertCommits(repoKey, solutionRoot, diffResult.Commits, databaseName);
        }

        var discoveredFiles = await discoveryService.GetFilesToProcess(sln, solution, extensionsToInclude);
        var filesToProcess = FilterFiles(discoveredFiles, diffResult?.ModifiedFiles);

        var totalFiles = filesToProcess.Length;
        var currentFileIndex = 0;

        var symbolBuffer = new List<Symbol>(batchSize);
        var relBuffer = new List<Relationship>(batchSize);

        foreach (var file in filesToProcess)
        {
            currentFileIndex++;
            await ProcessFile(file, solutionRoot, repoKey, databaseName, batchSize, symbolBuffer, relBuffer, currentFileIndex, totalFiles, minAccessibility);
        }

        if (symbolBuffer.Count > 0)
        {
            logger.LogInformation("Flushing final {SymbolCount} symbols and {RelCount} relationships to Neo4j...", symbolBuffer.Count, relBuffer.Count);
            await graphService.Flush(symbolBuffer, relBuffer, databaseName);
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

        if (changedFiles is not null && changedFiles.Any())
        {
            result = result
                .Where(f => changedFiles.Contains(f.FilePath))
                .ToArray();
        }
        else if (changedFiles is not null)
        {
            // If changedFiles is an empty collection (not null), it means we ARE in incremental mode
            // but no files matched. So we return empty.
            result = Array.Empty<ProcessedFile>();
        }

        return result;
    }

    private async ValueTask<DiffResult?> GetChangedFiles(FileInfo sln, string? diffBase, bool force, HashSet<string> includeExtensions)
    {
        if (diffBase is null) return null;

        var result = await versionControlService.GetChangedFiles(diffBase, sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory(), includeExtensions);

        if (force)
        {
            logger.LogInformation("Incremental indexing bypassed due to --force flag, but commits will still be ingested.");
            return result with { ModifiedFiles = new HashSet<string>(), DeletedFiles = new HashSet<string>() };
        }

        logger.LogInformation("Incremental indexing enabled. Found {ModifiedCount} modified and {DeletedCount} deleted files since {DiffBase}", result.ModifiedFiles.Count, result.DeletedFiles.Count, diffBase);

        return result;
    }

    private async ValueTask ProcessFile(
        ProcessedFile file,
        string solutionRoot,
        string repoKey,
        string databaseName,
        int batchSize,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        int currentFileIndex,
        int totalFiles,
        Accessibility minAccessibility)
    {
        var filePath = file.FilePath;
        var relativePath = fileSystem.Path.GetRelativePath(solutionRoot, filePath).Replace('\\', '/');

        progressService.ReportProgress(currentFileIndex, totalFiles, relativePath);
        logger.LogDebug("Processing file: {FilePath}", filePath);

        var fileKey = $"{repoKey}:{filePath}";
        var fileHash = fileService.ComputeSha256(await fileSystem.File.ReadAllBytesAsync(filePath));
        var metadata = await versionControlService.GetFileMetadata(filePath, solutionRoot);

        await graphService.UpsertFile(fileKey, filePath, fileHash, metadata, repoKey, databaseName);
        await graphService.DeletePriorSymbols(fileKey, databaseName);

        var handler = handlers.FirstOrDefault(h => h.CanHandle(filePath));
        if (handler != null)
        {
            await handler.HandleAsync(file.Document, file.Compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
        }
        else
        {
            logger.LogDebug("No handler found for file: {FilePath}", filePath);
        }

        if (symbolBuffer.Count >= batchSize)
        {
            logger.LogInformation("Flushing {SymbolCount} symbols and {RelCount} relationships to Neo4j...", symbolBuffer.Count, relBuffer.Count);
            await graphService.Flush(symbolBuffer, relBuffer, databaseName);
            symbolBuffer.Clear();
            relBuffer.Clear();
        }

        logger.LogDebug("Indexed {FilePath}", filePath);
    }
}