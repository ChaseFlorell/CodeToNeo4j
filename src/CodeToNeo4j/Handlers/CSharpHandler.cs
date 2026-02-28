using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CodeToNeo4j.Neo4j;

namespace CodeToNeo4j.Handlers;

public class CSharpHandler(ISymbolMapper symbolMapper) : IDocumentHandler
{
    public bool CanHandle(string filePath) => filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    public async ValueTask HandleAsync(
        Document document,
        Compilation compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
        string databaseName)
    {
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree is null) return;

        var rootNode = await syntaxTree.GetRootAsync();
        var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

        foreach (var typeDecl in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            ProcessTypeDeclaration(typeDecl, semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer);
        }
    }

    private void ProcessTypeDeclaration(
        BaseTypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer)
    {
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol is null) return;

        var typeRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, typeSymbol, typeDecl);
        symbolBuffer.Add(typeRec);

        switch (typeDecl)
        {
            case TypeDeclarationSyntax tds:
            {
                ProcessTypeDeclarationSyntax(semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, tds, typeRec);
                break;
            }
            case EnumDeclarationSyntax eds:
            {
                ProcessEnumDeclarationSyntax(semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, eds, typeRec);
                break;
            }
        }
    }

    private void ProcessEnumDeclarationSyntax(SemanticModel semanticModel, string repoKey, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer, EnumDeclarationSyntax eds, SymbolRecord typeRec)
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

    private void ProcessTypeDeclarationSyntax(SemanticModel semanticModel, string repoKey, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer, TypeDeclarationSyntax tds, SymbolRecord typeRec)
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
}
