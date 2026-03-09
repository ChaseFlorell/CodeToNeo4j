using System.IO.Abstractions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.FileHandlers;

public class CSharpHandler(
    ISymbolMapper symbolMapper,
    IFileSystem fileSystem)
    : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".cs";

    protected override async Task HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        if (document is Document doc && compilation is not null)
        {
            var syntaxTree = await doc.GetSyntaxTreeAsync().ConfigureAwait(false);
            if (syntaxTree is not null)
            {
                var rootNode = await syntaxTree.GetRootAsync().ConfigureAwait(false);
                var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

                foreach (var typeDecl in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
                {
                    ProcessTypeDeclaration(typeDecl, semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
                }
            }
        }
    }

    private void ProcessTypeDeclaration(
        BaseTypeDeclarationSyntax typeDecl,
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        if (semanticModel.GetDeclaredSymbol(typeDecl) 
                is INamedTypeSymbol typeSymbol 
            && (typeSymbol.DeclaredAccessibility >= minAccessibility 
                || typeSymbol.DeclaredAccessibility == Accessibility.NotApplicable))
        {
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
    }

    private void ProcessEnumDeclarationSyntax(SemanticModel semanticModel, string? repoKey, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, EnumDeclarationSyntax eds, Symbol typeRec, Accessibility minAccessibility)
    {
        foreach (var member in eds.Members)
        {
            var memberSymbol = semanticModel.GetDeclaredSymbol(member);

            if (memberSymbol is not null
                && (memberSymbol.DeclaredAccessibility >= minAccessibility
                    || memberSymbol.DeclaredAccessibility == Accessibility.NotApplicable))
            {
                var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, member);
                symbolBuffer.Add(memberRec);

                relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));
            }
        }
    }

    private void ProcessTypeDeclarationSyntax(
        SemanticModel semanticModel,
        string? repoKey,
        string fileKey,
        string filePath,
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
                    filePath,
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
                    filePath,
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
        string filePath,
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
                    filePath,
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
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Symbol typeRec,
        Accessibility minAccessibility)
    {
        if (IsAccessibilityBelowMinimum(memberSymbol, minAccessibility))
        {
            return;
        }

        var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, memberSyntax);
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