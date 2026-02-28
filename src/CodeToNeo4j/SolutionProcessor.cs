using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.IO.Abstractions;
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
    ISymbolMapper symbolMapper,
    IFileSystem fileSystem,
    IProgressService progressService,
    ILogger<SolutionProcessor> logger) : ISolutionProcessor
{
    public async Task ProcessSolutionAsync(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool force, bool skipDependencies)
    {
        logger.LogInformation("Processing solution: {SlnPath}", sln.FullName);

        var changedFiles = diffBase is null || force
            ? null
            : await gitService.GetChangedCsFilesAsync(diffBase, sln.Directory?.FullName ?? fileSystem.Directory.GetCurrentDirectory());

        if (changedFiles is not null)
        {
            logger.LogInformation("Incremental indexing enabled. Found {Count} changed .cs files since {DiffBase}", changedFiles.Count, diffBase);
        }
        else if (diffBase is not null && force)
        {
            logger.LogInformation("Incremental indexing bypassed due to --force flag.");
        }

        await neo4jService.VerifyNeo4JVersionAsync();
        logger.LogInformation("Neo4j version verified.");

        await neo4jService.EnsureSchemaAsync(databaseName);
        logger.LogInformation("Neo4j schema ensured for database: {DatabaseName}", databaseName);

        await neo4jService.UpsertProjectAsync(repoKey, databaseName);
        logger.LogInformation("Project upserted: {RepoKey}", repoKey);

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => { logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message); });

        logger.LogInformation("Opening solution...");
        var solution = await workspace.OpenSolutionAsync(sln.FullName);
        logger.LogInformation("Solution opened successfully.");

        if (!skipDependencies)
        {
            logger.LogInformation("Ingesting NuGet dependencies...");
            var dependencies = new List<DependencyRecord>();
            foreach (var project in solution.Projects)
            {
                // We'll also try to get explicit package references if we can.
                // However, compilation.ReferencedAssemblyNames is a good source for all libraries.
                var compilation = await project.GetCompilationAsync();
                if (compilation is null) continue;

                foreach (var reference in compilation.ReferencedAssemblyNames)
                {
                    var key = $"pkg:{reference.Name}:{reference.Version}";
                    dependencies.Add(new DependencyRecord(key, reference.Name, reference.Version?.ToString() ?? "0.0.0.0"));
                }
            }

            // Deduplicate across projects
            var uniqueDeps = dependencies.DistinctBy(d => d.Key).ToList();
            await neo4jService.UpsertDependenciesAsync(repoKey, uniqueDeps, databaseName);
            logger.LogInformation("Ingested {Count} unique dependencies.", uniqueDeps.Count);
        }

        var allDocuments = solution.Projects
            .SelectMany(p => p.Documents)
            .Where(d => d.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ?? false)
            .ToList();

        if (changedFiles is not null)
        {
            allDocuments = allDocuments
                .Where(d => changedFiles.Contains(fileService.NormalizePath(d.FilePath!)))
                .ToList();
        }

        var totalFiles = allDocuments.Count;
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

            var projectDocuments = allDocuments.Where(d => d.Project.Id == project.Id).ToList();

            foreach (var document in projectDocuments)
            {
                currentFileIndex++;
                var filePath = fileService.NormalizePath(document.FilePath!);

                progressService.ReportProgress(currentFileIndex, totalFiles, filePath);
                logger.LogDebug("Processing file: {FilePath}", filePath);

                var syntaxTree = await document.GetSyntaxTreeAsync();
                if (syntaxTree is null) continue;

                var rootNode = await syntaxTree.GetRootAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

                var fileKey = $"{repoKey}:{filePath}";
                var fileHash = fileService.ComputeSha256(await fileSystem.File.ReadAllBytesAsync(filePath));

                await neo4jService.UpsertFileAsync(fileKey, filePath, fileHash, repoKey, databaseName);
                await neo4jService.DeletePriorSymbolsAsync(fileKey, databaseName);

                foreach (var typeDecl in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
                    if (typeSymbol is null) continue;

                    var typeRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, typeSymbol, typeDecl);
                    symbolBuffer.Add(typeRec);

                    if (typeDecl is TypeDeclarationSyntax tds)
                    {
                        foreach (var member in tds.Members)
                        {
                            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
                            if (memberSymbol is null) continue;

                            var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member);
                            symbolBuffer.Add(memberRec);

                            relBuffer.Add(new RelRecord(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
                        }
                    }
                    else if (typeDecl is EnumDeclarationSyntax eds)
                    {
                        foreach (var member in eds.Members)
                        {
                            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
                            if (memberSymbol is null) continue;

                            var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member);
                            symbolBuffer.Add(memberRec);

                            relBuffer.Add(new RelRecord(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
                        }
                    }
                }

                if (symbolBuffer.Count >= batchSize)
                {
                    logger.LogInformation("Flushing {SymbolCount} symbols and {RelCount} relationships to Neo4j...", symbolBuffer.Count, relBuffer.Count);
                    await neo4jService.FlushAsync(repoKey, fileKey, symbolBuffer, relBuffer, databaseName);
                }

                logger.LogDebug("Indexed {FilePath}", filePath);
            }
        }

        if (symbolBuffer.Count > 0)
        {
            logger.LogInformation("Flushing final {SymbolCount} symbols and {RelCount} relationships to Neo4j...", symbolBuffer.Count, relBuffer.Count);
            await neo4jService.FlushAsync(repoKey, null, symbolBuffer, relBuffer, databaseName);
        }

        logger.LogInformation("Done.");
    }
}