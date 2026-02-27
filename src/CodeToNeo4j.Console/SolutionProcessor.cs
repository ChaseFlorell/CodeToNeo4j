using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeToNeo4j.Console;

public interface ISolutionProcessor
{
    Task ProcessSolutionAsync(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize);
}

public class SolutionProcessor(
    IGitService gitService,
    INeo4jService neo4jService,
    IFileService fileService,
    ISymbolMapper symbolMapper) : ISolutionProcessor
{
    public async Task ProcessSolutionAsync(FileInfo sln, string repoKey, string? diffBase, string databaseName, int batchSize)
    {
        var changedFiles = diffBase is null
            ? null
            : await gitService.GetChangedCsFilesAsync(diffBase, Directory.GetCurrentDirectory());

        await neo4jService.VerifyNeo4JVersionAsync();
        await neo4jService.EnsureSchemaAsync(databaseName);
        await neo4jService.UpsertProjectAsync(repoKey);

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(e => { System.Console.Error.WriteLine($"Workspace warning: {e.Diagnostic.Message}"); });

        var solution = await workspace.OpenSolutionAsync(sln.FullName);

        var symbolBuffer = new List<SymbolRecord>(batchSize);
        var relBuffer = new List<RelRecord>(batchSize);

        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null) continue;

            foreach (var document in project.Documents)
            {
                if (!document.FilePath?.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ?? true) continue;

                var filePath = fileService.NormalizePath(document.FilePath!);
                if (changedFiles is not null && !changedFiles.Contains(filePath)) continue;

                var syntaxTree = await document.GetSyntaxTreeAsync();
                if (syntaxTree is null) continue;

                var rootNode = await syntaxTree.GetRootAsync();
                var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

                var fileKey = $"{repoKey}:{filePath}";
                var fileHash = fileService.ComputeSha256(await File.ReadAllBytesAsync(filePath));

                await neo4jService.UpsertFileAsync(fileKey, filePath, fileHash, repoKey);
                await neo4jService.DeletePriorSymbolsAsync(fileKey);

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
                    await neo4jService.FlushAsync(repoKey, fileKey, symbolBuffer, relBuffer);
                }

                System.Console.WriteLine($"Indexed {filePath}");
            }
        }

        if (symbolBuffer.Count > 0)
        {
            await neo4jService.FlushAsync(repoKey, null, symbolBuffer, relBuffer);
        }

        System.Console.WriteLine("Done.");
    }
}
