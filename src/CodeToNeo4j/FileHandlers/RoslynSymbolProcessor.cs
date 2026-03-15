using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.FileHandlers;

public interface IRoslynSymbolProcessor
{
    void ProcessSyntaxTree(
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility);
}

public class RoslynSymbolProcessor(
    ISymbolMapper symbolMapper,
    IMemberDependencyExtractor dependencyExtractor) : IRoslynSymbolProcessor
{
    public void ProcessSyntaxTree(
        SyntaxTree syntaxTree,
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        var rootNode = syntaxTree.GetRoot();

        // Process using directives for dependencies
        foreach (var usingDirective in rootNode.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (usingDirective.Name == null) continue;
            var symbol = semanticModel.GetSymbolInfo(usingDirective.Name).Symbol;

            if (symbol != null)
            {
                dependencyExtractor.AddDependsOnIfExternal(symbol, semanticModel.Compilation.Assembly, repoKey, fileKey, relBuffer);
            }
            else
            {
                var depKey = $"{repoKey}:{usingDirective.Name}";
                relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: depKey, RelType: "DEPENDS_ON"));
            }
        }

        // Process global usings from the compilation (for the current file)
        if (semanticModel.Compilation is CSharpCompilation)
        {
            foreach (var tree in semanticModel.Compilation.SyntaxTrees)
            {
                if (string.Equals(tree.FilePath, syntaxTree.FilePath, StringComparison.OrdinalIgnoreCase)) continue;

                var root = tree.GetRoot();
                foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    if (u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && u.Name != null)
                    {
                        var globalSymbol = semanticModel.Compilation.GetSemanticModel(tree).GetSymbolInfo(u.Name).Symbol;

                        if (globalSymbol == null)
                        {
                            var depKey = $"{repoKey}:{u.Name}";
                            if (!relBuffer.Any(r => r.FromKey == fileKey && r.ToKey == depKey && r.RelType == "DEPENDS_ON"))
                            {
                                relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: depKey, RelType: "DEPENDS_ON"));
                            }
                        }
                        else
                        {
                            dependencyExtractor.AddDependsOnIfExternal(globalSymbol, semanticModel.Compilation.Assembly, repoKey, fileKey, relBuffer);
                        }
                    }
                }
            }
        }

        foreach (var typeDecl in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (string.IsNullOrEmpty(fileNamespace))
            {
                var symbol = semanticModel.GetDeclaredSymbol(typeDecl);
                fileNamespace = symbol?.ContainingNamespace?.ToDisplayString();
            }

            ProcessTypeDeclaration(typeDecl, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        }
    }

    private void ProcessTypeDeclaration(
        BaseTypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        if (semanticModel.GetDeclaredSymbol(typeDecl)
                is INamedTypeSymbol typeSymbol
            && (typeSymbol.DeclaredAccessibility >= minAccessibility
                || typeSymbol.DeclaredAccessibility == Accessibility.NotApplicable))
        {
            var typeRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, relativePath, fileNamespace, typeSymbol, typeDecl);
            symbolBuffer.Add(typeRec);

            switch (typeDecl)
            {
                case TypeDeclarationSyntax tds:
                {
                    ProcessTypeDeclarationSyntax(semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, tds, typeRec, minAccessibility);
                    break;
                }
                case EnumDeclarationSyntax eds:
                {
                    ProcessEnumDeclarationSyntax(semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, eds, typeRec, minAccessibility);
                    break;
                }
            }
        }
    }

    private void ProcessEnumDeclarationSyntax(SemanticModel semanticModel, string? repoKey, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, EnumDeclarationSyntax eds, Symbol typeRec, Accessibility minAccessibility)
    {
        foreach (var member in eds.Members)
        {
            var memberSymbol = semanticModel.GetDeclaredSymbol(member);

            if (memberSymbol is not null
                && (memberSymbol.DeclaredAccessibility >= minAccessibility
                    || memberSymbol.DeclaredAccessibility == Accessibility.NotApplicable))
            {
                var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, relativePath, fileNamespace, memberSymbol, member);
                symbolBuffer.Add(memberRec);

                relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
            }
        }
    }

    private void ProcessTypeDeclarationSyntax(
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        TypeDeclarationSyntax tds,
        Symbol typeRec,
        Accessibility minAccessibility)
    {
        foreach (var member in tds.Members)
        {
            if (member is EventFieldDeclarationSyntax efds)
            {
                ProcessEventFieldDeclaration(efds, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, typeRec, minAccessibility);
                continue;
            }

            if (member is FieldDeclarationSyntax fds)
            {
                ProcessFieldDeclaration(fds, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, typeRec, minAccessibility);
                continue;
            }

            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
            if (memberSymbol is null && member is EventDeclarationSyntax eventDecl)
            {
                memberSymbol = semanticModel.GetDeclaredSymbol(eventDecl);
            }

            if (memberSymbol is not null)
            {
                ProcessMemberSymbol(memberSymbol, member, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, typeRec, minAccessibility);
            }
        }
    }

    private void ProcessFieldDeclaration(
        FieldDeclarationSyntax fds,
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Symbol typeRec,
        Accessibility minAccessibility)
    {
        foreach (var variable in fds.Declaration.Variables)
        {
            if (semanticModel.GetDeclaredSymbol(variable) is IFieldSymbol variableSymbol)
            {
                ProcessMemberSymbol(variableSymbol, variable, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, typeRec, minAccessibility);
            }
        }
    }

    private void ProcessEventFieldDeclaration(
        EventFieldDeclarationSyntax efds,
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Symbol typeRec,
        Accessibility minAccessibility)
    {
        foreach (var variable in efds.Declaration.Variables)
        {
            if (semanticModel.GetDeclaredSymbol(variable) is IEventSymbol variableSymbol)
            {
                ProcessMemberSymbol(variableSymbol, variable, semanticModel, repoKey, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, typeRec, minAccessibility);
            }
        }
    }

    private void ProcessMemberSymbol(
        ISymbol memberSymbol,
        SyntaxNode memberSyntax,
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Symbol typeRec,
        Accessibility minAccessibility)
    {
        if (AccessibilityFilter.IsAccessibilityBelowMinimum(memberSymbol, minAccessibility))
        {
            return;
        }

        var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, relativePath, fileNamespace, memberSymbol, memberSyntax);
        symbolBuffer.Add(memberRec);

        relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));

        dependencyExtractor.ExtractMemberDependencies(memberSymbol, memberSyntax, semanticModel, repoKey, relBuffer, typeRec, memberRec);
    }
}
