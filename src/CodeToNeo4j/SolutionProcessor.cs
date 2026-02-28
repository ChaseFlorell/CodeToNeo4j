using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using System.IO.Abstractions;
using CodeToNeo4j.FileHandlers;
using Microsoft.Extensions.Logging;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Git;
using CodeToNeo4j.Progress;
using CodeToNeo4j.Neo4j;

namespace CodeToNeo4j;

public class SolutionProcessor(
    IGitService gitService,
    INeo4jService neo4jService,
    IFileService fileService,
    IFileSystem fileSystem,
    IProgressService progressService,
    IEnumerable<IDocumentHandler> handlers,
    ILogger<SolutionProcessor> logger) : ISolutionProcessor
{
    public async ValueTask ProcessSolution(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool force, bool skipDependencies, Accessibility minAccessibility, IEnumerable<string> includeExtensions)
    {
        var solutionRoot = sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory();
        logger.LogInformation("Processing solution: {SlnPath}", sln.FullName);
        await InitializeNeo4j(repoKey, databaseName);

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message));

        logger.LogInformation("Opening solution...");
        var solution = await workspace.OpenSolutionAsync(sln.FullName);
        logger.LogInformation("Solution opened successfully.");

        if (!skipDependencies)
        {
            await IngestDependencies(solution, repoKey, databaseName);
        }

        var diffResult = await GetChangedFiles(sln, diffBase, force, includeExtensions);
        
        if (diffResult?.DeletedFiles.Count > 0)
        {
            logger.LogInformation("Deleting {Count} files that were removed in git...", diffResult.DeletedFiles.Count);
            foreach (var deletedFile in diffResult.DeletedFiles)
            {
                var fileKey = $"{repoKey}:{fileService.GetRelativePath(solutionRoot, deletedFile)}";
                await neo4jService.DeleteFile(fileKey, databaseName);
            }
        }

        var filesToProcess = GetFilesToProcess(sln, solution, diffResult?.ModifiedFiles, includeExtensions);

        var totalFiles = filesToProcess.Length;
        var currentFileIndex = 0;

        var symbolBuffer = new List<SymbolRecord>(batchSize);
        var relBuffer = new List<RelRecord>(batchSize);

        foreach (var file in filesToProcess)
        {
            currentFileIndex++;
            await ProcessFile(file, solutionRoot, repoKey, databaseName, batchSize, symbolBuffer, relBuffer, currentFileIndex, totalFiles, minAccessibility);
        }

        if (symbolBuffer.Count > 0)
        {
            logger.LogInformation("Flushing final {SymbolCount} symbols and {RelCount} relationships to Neo4j...", symbolBuffer.Count, relBuffer.Count);
            await neo4jService.Flush(repoKey, null, symbolBuffer, relBuffer, databaseName);
        }

        logger.LogInformation("Done.");
    }

    private record ProcessedFile(string FilePath, Document? Document, Compilation? Compilation);

    private async ValueTask<GitDiffResult?> GetChangedFiles(FileInfo sln, string? diffBase, bool force, IEnumerable<string> includeExtensions)
    {
        var result = diffBase is null || force
            ? null
            : await gitService.GetChangedFiles(diffBase, sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory(), includeExtensions);

        if (result is not null)
        {
            logger.LogInformation("Incremental indexing enabled. Found {ModifiedCount} modified and {DeletedCount} deleted files since {DiffBase}", result.ModifiedFiles.Count, result.DeletedFiles.Count, diffBase);
        }
        else if (diffBase is not null && force)
        {
            logger.LogInformation("Incremental indexing bypassed due to --force flag.");
        }

        return result;
    }

    private async ValueTask InitializeNeo4j(string repoKey, string databaseName)
    {
        await neo4jService.VerifyNeo4jVersion();
        logger.LogInformation("Neo4j version verified.");

        await neo4jService.EnsureSchema(databaseName);
        logger.LogInformation("Neo4j schema ensured for database: {DatabaseName}", databaseName);

        await neo4jService.UpsertProject(repoKey, databaseName);
        logger.LogInformation("Project upserted: {RepoKey}", repoKey);
    }

    private async ValueTask IngestDependencies(Solution solution, string repoKey, string databaseName)
    {
        logger.LogInformation("Ingesting NuGet dependencies...");
        var dependencies = new List<DependencyRecord>();
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null) continue;

            foreach (var reference in compilation.ReferencedAssemblyNames)
            {
                var key = $"pkg:{reference.Name}:{reference.Version}";
                dependencies.Add(new DependencyRecord(key, reference.Name, reference.Version.ToString()));
            }
        }

        var uniqueDeps = dependencies.DistinctBy(d => d.Key).ToArray();
        await neo4jService.UpsertDependencies(repoKey, uniqueDeps, databaseName);
        logger.LogInformation("Ingested {Count} unique dependencies.", uniqueDeps.Length);
    }

    private ProcessedFile[] GetFilesToProcess(FileInfo sln, Solution solution, IEnumerable<string>? changedFiles, IEnumerable<string> includeExtensions)
    {
        var solutionFiles = new Dictionary<string, ProcessedFile>(StringComparer.OrdinalIgnoreCase);
        var extensionSet = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 1. Get all documents from MSBuild
        foreach (var project in solution.Projects)
        {
            var compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
            
            // Regular Documents
            foreach (var doc in project.Documents)
            {
                var path = fileService.NormalizePath(doc.FilePath!);
                if (string.IsNullOrEmpty(path)) continue;
                if (!extensionSet.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

                if (!solutionFiles.ContainsKey(path))
                {
                    solutionFiles[path] = new ProcessedFile(path, doc, compilation);
                }
            }

            // Additional Documents
            foreach (var doc in project.AdditionalDocuments)
            {
                var path = fileService.NormalizePath(doc.FilePath!);
                if (string.IsNullOrEmpty(path)) continue;
                if (!extensionSet.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

                if (!solutionFiles.ContainsKey(path))
                {
                    // Since AdditionalDocuments are TextDocuments, and Documents are also TextDocuments,
                    // we can't easily cast without checking. But Document? in ProcessedFile currently 
                    // only accepts Document. We should change it to TextDocument.
                    // However, CSharpHandler specifically wants Document.
                    // Let's try to keep it as Document for C# and TextDocument for others?
                    // Or just use Document and only populate it if it's a real Document.
                    solutionFiles[path] = new ProcessedFile(path, doc as Document, null);
                }
            }
        }

        // 2. File system fallback for other files in the solution directory
        var solutionDir = sln.DirectoryName!;
        var allFilesOnDisk = fileSystem.Directory.EnumerateFiles(solutionDir, "*.*", SearchOption.AllDirectories);
        foreach (var fileOnDisk in allFilesOnDisk)
        {
            var normalizedPath = fileService.NormalizePath(fileOnDisk);
            if (IsExcluded(normalizedPath)) continue;
            if (!extensionSet.Any(ext => normalizedPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) continue;

            if (!solutionFiles.ContainsKey(normalizedPath))
            {
                solutionFiles[normalizedPath] = new ProcessedFile(normalizedPath, null, null);
            }
        }

        var result = solutionFiles.Values.ToArray();

        if (changedFiles is not null)
        {
            result = result
                .Where(f => changedFiles.Contains(f.FilePath))
                .ToArray();
        }

        return result;
    }

    private bool IsExcluded(string path)
    {
        var parts = path.Split('/');
        return parts.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
                             p.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
                             p.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
                             p.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
                             p.Equals("node_modules", StringComparison.OrdinalIgnoreCase));
    }

    private async ValueTask ProcessFile(
        ProcessedFile file,
        string solutionRoot,
        string repoKey,
        string databaseName,
        int batchSize,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
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

        await neo4jService.UpsertFile(fileKey, filePath, fileHash, repoKey, databaseName);
        await neo4jService.DeletePriorSymbols(fileKey, databaseName);

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
            await neo4jService.Flush(repoKey, fileKey, symbolBuffer, relBuffer, databaseName);
            symbolBuffer.Clear();
            relBuffer.Clear();
        }

        logger.LogDebug("Indexed {FilePath}", filePath);
    }
}