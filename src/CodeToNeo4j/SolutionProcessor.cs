using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Git;
using CodeToNeo4j.Progress;
using CodeToNeo4j.Handlers;
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
    public async ValueTask ProcessSolution(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool force, bool skipDependencies)
    {
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

        var changedFiles = await GetChangedFiles(sln, diffBase, force);
        var allDocuments = GetDocumentsToProcess(solution, changedFiles);

        var totalFiles = allDocuments.Length;
        var currentFileIndex = 0;

        var symbolBuffer = new List<SymbolRecord>(batchSize);
        var relBuffer = new List<RelRecord>(batchSize);

        foreach (var project in solution.Projects)
        {
            logger.LogInformation("Processing project: {ProjectName}", project.Name);
            var compilation = await project.GetCompilationAsync();
            if (compilation is null)
            {
                logger.LogWarning("Could not get compilation for project: {ProjectName}", project.Name);
                continue;
            }

            var projectDocuments = allDocuments.Where(d => d.Project.Id == project.Id).ToArray();

            foreach (var document in projectDocuments)
            {
                currentFileIndex++;
                await ProcessDocument(document, compilation, repoKey, databaseName, batchSize, symbolBuffer, relBuffer, currentFileIndex, totalFiles);
            }
        }

        if (symbolBuffer.Count > 0)
        {
            logger.LogInformation("Flushing final {SymbolCount} symbols and {RelCount} relationships to Neo4j...", symbolBuffer.Count, relBuffer.Count);
            await neo4jService.Flush(repoKey, null, symbolBuffer, relBuffer, databaseName);
        }

        logger.LogInformation("Done.");
    }

    private async ValueTask<IEnumerable<string>?> GetChangedFiles(FileInfo sln, string? diffBase, bool force)
    {
        var changedFiles = diffBase is null || force
            ? null
            : await gitService.GetChangedFiles(diffBase, sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory());

        if (changedFiles is not null)
        {
            logger.LogInformation("Incremental indexing enabled. Found {Count} changed files since {DiffBase}", changedFiles.Count, diffBase);
        }
        else if (diffBase is not null && force)
        {
            logger.LogInformation("Incremental indexing bypassed due to --force flag.");
        }

        return changedFiles;
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

    private Document[] GetDocumentsToProcess(Solution solution, IEnumerable<string>? changedFiles)
    {
        var allDocuments = solution.Projects
            .SelectMany(p => p.Documents)
            .Where(d =>
                d.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) == true ||
                d.FilePath?.EndsWith(".razor", StringComparison.OrdinalIgnoreCase) == true ||
                d.FilePath?.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        if (changedFiles is not null)
        {
            allDocuments = allDocuments
                .Where(d => changedFiles.Contains(fileService.NormalizePath(d.FilePath!)))
                .ToArray();
        }

        return allDocuments;
    }

    private async ValueTask ProcessDocument(
        Document document,
        Compilation compilation,
        string repoKey,
        string databaseName,
        int batchSize,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
        int currentFileIndex,
        int totalFiles)
    {
        var filePath = fileService.NormalizePath(document.FilePath!);

        progressService.ReportProgress(currentFileIndex, totalFiles, filePath);
        logger.LogDebug("Processing file: {FilePath}", filePath);

        var fileKey = $"{repoKey}:{filePath}";
        var fileHash = fileService.ComputeSha256(await fileSystem.File.ReadAllBytesAsync(filePath));

        await neo4jService.UpsertFile(fileKey, filePath, fileHash, repoKey, databaseName);
        await neo4jService.DeletePriorSymbols(fileKey, databaseName);

        var handler = handlers.FirstOrDefault(h => h.CanHandle(filePath));
        if (handler != null)
        {
            await handler.HandleAsync(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName);
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