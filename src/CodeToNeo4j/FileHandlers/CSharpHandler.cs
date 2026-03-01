using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.FileHandlers;

public class CSharpHandler(ISymbolMapper symbolMapper) : DocumentHandlerBase
{
    public override string FileType => "C#";
    public override bool CanHandle(string filePath) => filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    public override async ValueTask HandleAsync(
        TextDocument? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        string databaseName,
        Accessibility minAccessibility)
    {
        await base.HandleAsync(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
        var doc = document as Document;
        if (doc is null || compilation is null) return;
        
        var syntaxTree = await doc.GetSyntaxTreeAsync();
        if (syntaxTree is null) return;

        var rootNode = await syntaxTree.GetRootAsync();
        var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

        foreach (var typeDecl in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            ProcessTypeDeclaration(typeDecl, semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        }
    }

    private void ProcessTypeDeclaration(
        BaseTypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl) as INamedTypeSymbol;
        if (typeSymbol is null) return;

        if (typeSymbol.DeclaredAccessibility < minAccessibility && typeSymbol.DeclaredAccessibility != Accessibility.NotApplicable)
        {
            return;
        }

        var typeRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, typeSymbol, typeDecl);
        symbolBuffer.Add(typeRec);

        switch (typeDecl)
        {
            case TypeDeclarationSyntax tds:
            {
                ProcessTypeDeclarationSyntax(semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, tds, typeRec, minAccessibility);
                break;
            }
            case EnumDeclarationSyntax eds:
            {
                ProcessEnumDeclarationSyntax(semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, eds, typeRec, minAccessibility);
                break;
            }
        }
    }

    private void ProcessEnumDeclarationSyntax(SemanticModel semanticModel, string repoKey, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, EnumDeclarationSyntax eds, Symbol typeRec, Accessibility minAccessibility)
    {
        foreach (var member in eds.Members)
        {
            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
            if (memberSymbol is null) continue;

            if (memberSymbol.DeclaredAccessibility < minAccessibility && memberSymbol.DeclaredAccessibility != Accessibility.NotApplicable)
            {
                continue;
            }

            var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member);
            symbolBuffer.Add(memberRec);

            relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
        }
    }

    private void ProcessTypeDeclarationSyntax(SemanticModel semanticModel, string repoKey, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, TypeDeclarationSyntax tds, Symbol typeRec, Accessibility minAccessibility)
    {
        foreach (var member in tds.Members)
        {
            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
            if (memberSymbol is null) continue;

            if (memberSymbol.DeclaredAccessibility < minAccessibility && memberSymbol.DeclaredAccessibility != Accessibility.NotApplicable)
            {
                continue;
            }

            var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member);
            symbolBuffer.Add(memberRec);

            relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
        }
    }
}
