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

public class RoslynSymbolProcessor(ISymbolMapper symbolMapper) : IRoslynSymbolProcessor
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
                AddDependsOnIfExternal(symbol, semanticModel.Compilation.Assembly, repoKey, fileKey, relBuffer);
            }
            else
            {
                // If the symbol is null, it might be because the assembly is not referenced, 
                // but we still want to capture it if it's a using
                var depKey = $"{repoKey}:{usingDirective.Name}";
                relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: depKey, RelType: "DEPENDS_ON"));
            }
        }

        // Process global usings from the compilation (for the current file)
        if (semanticModel.Compilation is CSharpCompilation)
        {
            // Usings from the current file are already processed.
            // We need to find usings that are NOT in the current file but are global usings in the compilation.
            // Roslyn doesn't make it super easy to find "all global usings that apply to this file but are defined elsewhere" 
            // through a simple public API without internal access.
            // However, we can look at all syntax trees in the compilation.
            foreach (var tree in semanticModel.Compilation.SyntaxTrees)
            {
                if (string.Equals(tree.FilePath, syntaxTree.FilePath, StringComparison.OrdinalIgnoreCase)) continue;

                var root = tree.GetRoot();
                foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
                {
                    if (u.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword) && u.Name != null)
                    {
                        var globalSymbol = semanticModel.Compilation.GetSemanticModel(tree).GetSymbolInfo(u.Name).Symbol;
                        
                        // Fallback if the symbol is null (assembly not referenced)
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
                            AddDependsOnIfExternal(globalSymbol, semanticModel.Compilation.Assembly, repoKey, fileKey, relBuffer);
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
                ProcessEventFieldDeclaration(efds,
                    semanticModel,
                    repoKey,
                    fileKey,
                    relativePath,
                    fileNamespace,
                    symbolBuffer,
                    relBuffer,
                    typeRec,
                    minAccessibility);

                continue;
            }

            if (member is FieldDeclarationSyntax fds)
            {
                ProcessFieldDeclaration(fds,
                    semanticModel,
                    repoKey,
                    fileKey,
                    relativePath,
                    fileNamespace,
                    symbolBuffer,
                    relBuffer,
                    typeRec,
                    minAccessibility);

                continue;
            }

            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
            if (memberSymbol is null && member is EventDeclarationSyntax eds)
            {
                memberSymbol = semanticModel.GetDeclaredSymbol(eds);
            }

            if (memberSymbol is not null)
            {
                ProcessMemberSymbol(memberSymbol,
                    member,
                    semanticModel,
                    repoKey,
                    fileKey,
                    relativePath,
                    fileNamespace,
                    symbolBuffer,
                    relBuffer,
                    typeRec,
                    minAccessibility);
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
                ProcessMemberSymbol(variableSymbol,
                    variable,
                    semanticModel,
                    repoKey,
                    fileKey,
                    relativePath,
                    fileNamespace,
                    symbolBuffer,
                    relBuffer,
                    typeRec,
                    minAccessibility);
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
                ProcessMemberSymbol(variableSymbol,
                    variable,
                    semanticModel,
                    repoKey,
                    fileKey,
                    relativePath,
                    fileNamespace,
                    symbolBuffer,
                    relBuffer,
                    typeRec,
                    minAccessibility);
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
        if (IsAccessibilityBelowMinimum(memberSymbol, minAccessibility))
        {
            return;
        }

        var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, relativePath, fileNamespace, memberSymbol, memberSyntax);
        symbolBuffer.Add(memberRec);

        relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));

        ExtractMemberDependencies(memberSymbol,
            memberSyntax,
            semanticModel,
            repoKey,
            relBuffer,
            typeRec);
    }

    private void ExtractMemberDependencies(
        ISymbol memberSymbol,
        SyntaxNode memberSyntax,
        SemanticModel semanticModel,
        string? repoKey,
        ICollection<Relationship> relBuffer,
        Symbol typeRec)
    {
        if (memberSyntax is BaseMethodDeclarationSyntax bmds)
        {
            ExtractMethodDependencies(bmds, semanticModel, repoKey, relBuffer, typeRec);
        }

        switch (memberSymbol)
        {
            case IPropertySymbol propertySymbol:
                ExtractPropertyDependencies(propertySymbol, repoKey, relBuffer, typeRec);
                break;
            case IEventSymbol eventSymbol:
                ExtractEventDependencies(eventSymbol, repoKey, relBuffer, typeRec);
                break;
            case IFieldSymbol fieldSymbol:
                ExtractFieldDependencies(fieldSymbol, repoKey, relBuffer, typeRec);
                break;
        }
    }

    private void ExtractMethodDependencies(
        BaseMethodDeclarationSyntax bmds,
        SemanticModel semanticModel,
        string? repoKey,
        ICollection<Relationship> relBuffer,
        Symbol typeRec)
    {
        foreach (var parameter in bmds.ParameterList.Parameters)
        {
            var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter) as IParameterSymbol;
            if (parameterSymbol?.Type is not (null or IErrorTypeSymbol))
            {
                AddDependsOnRelationship(typeRec.Key, parameterSymbol.Type, repoKey, relBuffer);
            }
        }

        if (semanticModel.GetDeclaredSymbol(bmds) is IMethodSymbol { ReturnType: not null and not IErrorTypeSymbol } baseMethodSymbol
            && baseMethodSymbol.MethodKind != MethodKind.Constructor)
        {
            AddDependsOnRelationship(typeRec.Key, baseMethodSymbol.ReturnType, repoKey, relBuffer);
        }
    }

    private void ExtractPropertyDependencies(
        IPropertySymbol propertySymbol,
        string? repoKey,
        ICollection<Relationship> relBuffer,
        Symbol typeRec)
    {
        if (propertySymbol.Type is not (null or IErrorTypeSymbol))
        {
            AddDependsOnRelationship(typeRec.Key, propertySymbol.Type, repoKey, relBuffer);
        }
    }

    private void ExtractEventDependencies(
        IEventSymbol eventSymbol,
        string? repoKey,
        ICollection<Relationship> relBuffer,
        Symbol typeRec)
    {
        var eventType = eventSymbol.Type;
        if (eventType is INamedTypeSymbol { IsGenericType: true, Name: "Nullable" } namedType)
        {
            eventType = namedType.TypeArguments[0];
        }

        AddDependsOnRelationship(typeRec.Key, eventType, repoKey, relBuffer);
    }

    private void ExtractFieldDependencies(
        IFieldSymbol fieldSymbol,
        string? repoKey,
        ICollection<Relationship> relBuffer,
        Symbol typeRec)
    {
        if (fieldSymbol.Type is not (null or IErrorTypeSymbol))
        {
            AddDependsOnRelationship(typeRec.Key, fieldSymbol.Type, repoKey, relBuffer);
        }
    }

    private void AddDependsOnIfExternal(
        ISymbol? symbol,
        IAssemblySymbol currentAssembly,
        string? repoKey,
        string fromKey,
        ICollection<Relationship> relBuffer)
    {
        if (symbol == null) return;

        if (symbol is INamespaceSymbol nsSymbol)
        {
            if (nsSymbol.ContainingAssembly != null && !SymbolEqualityComparer.Default.Equals(nsSymbol.ContainingAssembly, currentAssembly))
            {
                var depKey = $"{repoKey}:{nsSymbol.ToDisplayString()}";
                if (!relBuffer.Any(r => r.FromKey == fromKey && r.ToKey == depKey && r.RelType == "DEPENDS_ON"))
                {
                    relBuffer.Add(new Relationship(FromKey: fromKey, ToKey: depKey, RelType: "DEPENDS_ON"));
                }
            }
        }
        else if (symbol is INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingAssembly != null && !SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, currentAssembly))
            {
                var depKey = $"{repoKey}:{typeSymbol.ToDisplayString()}";
                if (!relBuffer.Any(r => r.FromKey == fromKey && r.ToKey == depKey && r.RelType == "DEPENDS_ON"))
                {
                    relBuffer.Add(new Relationship(FromKey: fromKey, ToKey: depKey, RelType: "DEPENDS_ON"));
                }
            }
        }
    }

    private void AddDependsOnRelationship(
        string fromKey,
        ITypeSymbol typeSymbol,
        string? repoKey,
        ICollection<Relationship> relBuffer)
    {
        var depKey = symbolMapper.BuildStableSymbolKey(repoKey, typeSymbol);
        relBuffer.Add(new Relationship(FromKey: fromKey, ToKey: depKey, RelType: "DEPENDS_ON"));
    }

    private static bool IsAccessibilityBelowMinimum(ISymbol symbol, Accessibility minAccessibility) =>
        symbol.DeclaredAccessibility < minAccessibility
        && symbol.DeclaredAccessibility != Accessibility.NotApplicable
        && !IsExplicitInterfaceImplementation(symbol);

    private static bool IsExplicitInterfaceImplementation(ISymbol symbol) =>
        symbol switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Any(),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Any(),
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.Any(),
            _ => false
        };
}
