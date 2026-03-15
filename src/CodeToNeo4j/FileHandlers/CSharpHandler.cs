using System.IO.Abstractions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.FileHandlers;

public class CSharpHandler(
    IRoslynSymbolProcessor symbolProcessor,
    IFileSystem fileSystem)
    : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".cs";

    protected override async Task<FileResult> HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        string relativePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        string? fileNamespace = null;
        if (document is Document doc && compilation is not null)
        {
            var syntaxTree = await doc.GetSyntaxTreeAsync().ConfigureAwait(false);
            if (syntaxTree is not null)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);
                
                // Get namespace for the file metadata
                var root = await syntaxTree.GetRootAsync().ConfigureAwait(false);
                var firstType = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().FirstOrDefault();
                if (firstType != null)
                {
                    var symbol = semanticModel.GetDeclaredSymbol(firstType);
                    fileNamespace = symbol?.ContainingNamespace?.ToDisplayString();
                }

                symbolProcessor.ProcessSyntaxTree(syntaxTree, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
            }
        }

        return new FileResult(fileNamespace, fileKey);
    }
}