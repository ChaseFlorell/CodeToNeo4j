using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System.IO.Abstractions;
using Microsoft.Extensions.Logging;
using CodeToNeo4j.Console.FileSystem;
using CodeToNeo4j.Console.Git;
using CodeToNeo4j.Console.Neo4j;

namespace CodeToNeo4j.Console;

public interface ISolutionProcessor
{
    Task ProcessSolutionAsync(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool force);
}

public class SolutionProcessor(
    IGitService gitService,
    INeo4jService neo4jService,
    IFileService fileService,
    ISymbolMapper symbolMapper,
    IFileSystem fileSystem,
    ILogger<SolutionProcessor> logger) : ISolutionProcessor
{
    public async Task ProcessSolutionAsync(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize, bool force)
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

            foreach (var document in project.Documents)
            {
                if (!document.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ?? true) continue;

                var filePath = fileService.NormalizePath(document.FilePath!);
                if (changedFiles is not null && !changedFiles.Contains(filePath)) continue;

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

                    var typeRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, typeSymbol, typeDecl.GetLocation());
                    symbolBuffer.Add(typeRec);

                    if (typeDecl is TypeDeclarationSyntax tds)
                    {
                        foreach (var member in tds.Members)
                        {
                            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
                            if (memberSymbol is null) continue;

                            var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member.GetLocation());
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

                            var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member.GetLocation());
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

                logger.LogInformation("Indexed {FilePath}", filePath);
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
