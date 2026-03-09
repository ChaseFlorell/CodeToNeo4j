using System.IO.Abstractions;
using System.Threading.Channels;
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
    public async Task ProcessSolution(FileInfo sln, string? repoKey, string? diffBase, string databaseName, int batchSize, bool skipDependencies, Accessibility minAccessibility, IEnumerable<string> includeExtensions)
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

        var discoveredFiles = discoveryService.GetFilesToProcess(sln, solution, extensionsToInclude);
        var filesToProcess = FilterFiles(discoveredFiles, diffResult?.ModifiedFiles);

        await versionControlService.LoadMetadata(solutionRoot, extensionsToInclude).ConfigureAwait(false);

        var totalFiles = filesToProcess.Length;
        var channel = Channel.CreateBounded<ProcessResult>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true
        });

        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 20 };
        var consumerTask = RunConsumer(channel.Reader, totalFiles, databaseName, batchSize);

        await Parallel.ForEachAsync(filesToProcess, parallelOptions, async (file, t) =>
        {
            var result = await ProcessFile(solution, file, solutionRoot, repoKey, minAccessibility).ConfigureAwait(false);
            await channel.Writer.WriteAsync(result, t).ConfigureAwait(false);
        }).ConfigureAwait(false);

        channel.Writer.Complete();
        var (totalSymbols, totalRelationships) = await consumerTask.ConfigureAwait(false);
        progressService.ProgressComplete();

        logger.LogInformation("Processing complete.");
        logger.LogInformation("Total nodes (symbols) created: {Count}", totalSymbols);
        logger.LogInformation("Total relationships created: {Count}", totalRelationships);

        foreach (var handler in handlers.Where(h => h.NumberOfFilesHandled > 0))
        {
            logger.LogInformation("{FileExtension} files handled: {Count}", handler.FileExtension, handler.NumberOfFilesHandled);
        }

        logger.LogInformation("Done.");
    }

    private async Task<(int TotalSymbols, int TotalRelationships)> RunConsumer(ChannelReader<ProcessResult> reader, int totalFiles, string databaseName, int batchSize)
    {
        var fileBuffer = new List<FileMetaData>(batchSize);
        var symbolBuffer = new List<Symbol>(batchSize);
        var relBuffer = new List<Relationship>(batchSize);
        var currentFileIndex = 0;
        var totalSymbols = 0;
        var totalRelationships = 0;

        await foreach (var result in reader.ReadAllAsync().ConfigureAwait(false))
        {
            fileBuffer.Add(result.File);
            symbolBuffer.AddRange(result.Symbols);
            relBuffer.AddRange(result.Relationships);

            totalSymbols += result.Symbols.Count;
            totalRelationships += result.Relationships.Count;

            if (fileBuffer.Count >= batchSize || symbolBuffer.Count >= batchSize)
            {
                await FlushBuffers(fileBuffer, symbolBuffer, relBuffer, databaseName).ConfigureAwait(false);
            }

            progressService.ReportProgress(++currentFileIndex, totalFiles, result.RelativePath);
        }

        if (fileBuffer.Count > 0 || symbolBuffer.Count > 0)
        {
            await FlushBuffers(fileBuffer, symbolBuffer, relBuffer, databaseName).ConfigureAwait(false);
        }

        return (totalSymbols, totalRelationships);
    }

    private async Task FlushBuffers(List<FileMetaData> files, List<Symbol> symbols, List<Relationship> relationships, string databaseName)
    {
        if (files.Count > 0)
        {
            await graphService.FlushFiles(files, databaseName).ConfigureAwait(false);
            files.Clear();
        }

        if (symbols.Count > 0 || relationships.Count > 0)
        {
            await graphService.FlushSymbols(symbols, relationships, databaseName).ConfigureAwait(false);
            symbols.Clear();
            relationships.Clear();
        }
    }

    private static ProcessedFile[] FilterFiles(IEnumerable<ProcessedFile> discoveredFiles, HashSet<string>? changedFiles)
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
                    result = [];
                }

                break;
            }
        }

        return result;
    }

    private async Task<DiffResult?> GetChangedFiles(FileInfo sln, string? diffBase, HashSet<string> includeExtensions)
    {
        if (diffBase is null) return null;

        var result = await versionControlService.GetChangedFiles(diffBase, sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory(), includeExtensions).ConfigureAwait(false);

        logger.LogInformation("Incremental indexing enabled. Found {ModifiedCount} modified and {DeletedCount} deleted files since {DiffBase}", result.ModifiedFiles.Count, result.DeletedFiles.Count, diffBase);

        return result;
    }

    private async Task<ProcessResult> ProcessFile(
        Microsoft.CodeAnalysis.Solution solution,
        ProcessedFile file,
        string solutionRoot,
        string? repoKey,
        Accessibility minAccessibility)
    {
        var filePath = file.FilePath;
        logger.LogDebug("Processing file: {FilePath}", filePath);

        var fileKey = $"{repoKey}:{filePath}";
        var fileHash = await fileService.ComputeSha256(filePath).ConfigureAwait(false);
        var metadata = await versionControlService.GetFileMetadata(filePath, solutionRoot).ConfigureAwait(false);

        var fileRecord = new FileMetaData(fileKey, filePath, fileHash, metadata, repoKey);

        var symbols = new List<Symbol>();
        var relationships = new List<Relationship>();

        TextDocument? document = null;
        Compilation? compilation = null;

        if (file.ProjectId != null)
        {
            var project = solution.GetProject(file.ProjectId);
            if (project != null)
            {
                if (file.DocumentId != null)
                {
                    document = (TextDocument?)project.GetDocument(file.DocumentId) ?? project.GetAdditionalDocument(file.DocumentId);
                }

                compilation = await project.GetCompilationAsync().ConfigureAwait(false);
            }
        }

        var handler = handlers.FirstOrDefault(h => h.CanHandle(filePath));
        if (handler != null)
        {
            await handler.Handle(document, compilation, repoKey, fileKey, filePath, symbols, relationships, minAccessibility).ConfigureAwait(false);
        }

        var relativePath = fileSystem.Path.GetRelativePath(solutionRoot, file.FilePath).Replace('\\', '/');
        return new ProcessResult(fileRecord, symbols, relationships, relativePath);
    }

    private record ProcessResult(
        FileMetaData File,
        List<Symbol> Symbols,
        List<Relationship> Relationships,
        string RelativePath);
}