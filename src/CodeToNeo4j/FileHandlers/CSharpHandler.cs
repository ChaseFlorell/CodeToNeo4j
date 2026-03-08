using System.IO.Abstractions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.FileHandlers;

public class CSharpHandler(ISymbolMapper symbolMapper, IFileSystem fileSystem) : DocumentHandlerBase(fileSystem)
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
        string databaseName,
        Accessibility minAccessibility)
    {
        var doc = document as Document;
        if (doc is null || compilation is null) return;

        var syntaxTree = await doc.GetSyntaxTreeAsync().ConfigureAwait(false);
        if (syntaxTree is null) return;

        var rootNode = await syntaxTree.GetRootAsync().ConfigureAwait(false);
        var semanticModel = compilation.GetSemanticModel(syntaxTree, ignoreAccessibility: true);

        foreach (var typeDecl in rootNode.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            ProcessTypeDeclaration(typeDecl, semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
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

    private void ProcessTypeDeclarationSyntax(SemanticModel semanticModel, string? repoKey, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, TypeDeclarationSyntax tds, Symbol typeRec, Accessibility minAccessibility)
    {
        foreach (var member in tds.Members)
        {
            if (member is EventFieldDeclarationSyntax efds)
            {
                foreach (var variable in efds.Declaration.Variables)
                {
                    var variableSymbol = semanticModel.GetDeclaredSymbol(variable) as IEventSymbol;
                    if (variableSymbol is null) continue;

                    ProcessMemberSymbol(variableSymbol, variable, semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, typeRec, minAccessibility);
                }
                continue;
            }

            var memberSymbol = semanticModel.GetDeclaredSymbol(member);
            if (memberSymbol is null)
            {
                if (member is EventDeclarationSyntax eds)
                {
                    memberSymbol = semanticModel.GetDeclaredSymbol(eds);
                }
            }

            if (memberSymbol is null) continue;

            ProcessMemberSymbol(memberSymbol, member, semanticModel, repoKey, fileKey, filePath, symbolBuffer, relBuffer, typeRec, minAccessibility);
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
        if (memberSymbol.DeclaredAccessibility < minAccessibility
            && memberSymbol.DeclaredAccessibility != Accessibility.NotApplicable
            && !IsExplicitInterfaceImplementation(memberSymbol))
        {
            return;
        }

        var memberRec = symbolMapper.ToSymbolRecord(repoKey, fileKey, filePath, memberSymbol, memberSyntax);
        symbolBuffer.Add(memberRec);

        relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: memberRec.Key, RelType: "CONTAINS"));

        if (memberSyntax is BaseMethodDeclarationSyntax bmds)
        {
            foreach (var parameter in bmds.ParameterList.Parameters)
            {
                var parameterSymbol = semanticModel.GetDeclaredSymbol(parameter) as IParameterSymbol;
                if (parameterSymbol is null) continue;

                var parameterType = parameterSymbol.Type;
                if (parameterType is null or IErrorTypeSymbol) continue;

                var depKey = symbolMapper.BuildStableSymbolKey(repoKey, parameterType);
                relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: depKey, RelType: "DEPENDS_ON"));
            }

            var baseMethodSymbol = semanticModel.GetDeclaredSymbol(bmds) as IMethodSymbol;
            if (baseMethodSymbol is { ReturnType: not null and not IErrorTypeSymbol }
                && baseMethodSymbol.MethodKind != MethodKind.Constructor)
            {
                var depKey = symbolMapper.BuildStableSymbolKey(repoKey, baseMethodSymbol.ReturnType);
                relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: depKey, RelType: "DEPENDS_ON"));
            }
        }
        else if (memberSymbol is IPropertySymbol propertySymbol)
        {
            if (propertySymbol.Type is not (null or IErrorTypeSymbol))
            {
                var depKey = symbolMapper.BuildStableSymbolKey(repoKey, propertySymbol.Type);
                relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: depKey, RelType: "DEPENDS_ON"));
            }
        }
        else if (memberSymbol is IEventSymbol eventSymbol)
        {
            if (eventSymbol.Type is not null)
            {
                var eventType = eventSymbol.Type;
                if (eventType is INamedTypeSymbol namedType && namedType.IsGenericType && namedType.Name == "Nullable")
                {
                    eventType = namedType.TypeArguments[0];
                }

                var depKey = symbolMapper.BuildStableSymbolKey(repoKey, eventType);
                relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: depKey, RelType: "DEPENDS_ON"));
            }
        }
        else if (memberSymbol is IFieldSymbol fieldSymbol)
        {
            if (fieldSymbol.Type is not (null or IErrorTypeSymbol))
            {
                var depKey = symbolMapper.BuildStableSymbolKey(repoKey, fieldSymbol.Type);
                relBuffer.Add(new Relationship(FromKey: typeRec.Key, ToKey: depKey, RelType: "DEPENDS_ON"));
            }
        }
    }

    private static bool IsExplicitInterfaceImplementation(ISymbol symbol)
    {
        return symbol switch
        {
            IMethodSymbol method => method.ExplicitInterfaceImplementations.Any(),
            IPropertySymbol property => property.ExplicitInterfaceImplementations.Any(),
            IEventSymbol @event => @event.ExplicitInterfaceImplementations.Any(),
            _ => false
        };
    }
}