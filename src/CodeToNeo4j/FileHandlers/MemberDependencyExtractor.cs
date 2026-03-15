using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.FileHandlers;

public class MemberDependencyExtractor(ISymbolMapper symbolMapper) : IMemberDependencyExtractor
{
    public void ExtractMemberDependencies(
        ISymbol memberSymbol,
        SyntaxNode memberSyntax,
        SemanticModel semanticModel,
        string? repoKey,
        ICollection<Relationship> relBuffer,
        Symbol typeRec,
        Symbol memberRec)
    {
        if (memberSyntax is BaseMethodDeclarationSyntax bmds)
        {
            ExtractMethodDependencies(bmds, semanticModel, repoKey, relBuffer, typeRec);
            ExtractMethodExecutes(bmds, memberRec, semanticModel, repoKey, relBuffer);
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

    public void AddDependsOnIfExternal(
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

    private void ExtractMethodExecutes(
        BaseMethodDeclarationSyntax bmds,
        Symbol callerRec,
        SemanticModel semanticModel,
        string? repoKey,
        ICollection<Relationship> relBuffer)
    {
        var body = (SyntaxNode?)bmds.Body ?? bmds.ExpressionBody;
        if (body == null) return;

        var seenCallees = new HashSet<string>();

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol calleeSymbol) continue;
            var calleeKey = symbolMapper.BuildStableSymbolKey(repoKey, calleeSymbol);
            if (seenCallees.Add(calleeKey))
                relBuffer.Add(new Relationship(FromKey: callerRec.Key, ToKey: calleeKey, RelType: "INVOKES"));
        }

        foreach (var objectCreation in body.DescendantNodes().OfType<BaseObjectCreationExpressionSyntax>())
        {
            if (semanticModel.GetSymbolInfo(objectCreation).Symbol is not IMethodSymbol ctorSymbol) continue;
            var calleeKey = symbolMapper.BuildStableSymbolKey(repoKey, ctorSymbol);
            if (seenCallees.Add(calleeKey))
                relBuffer.Add(new Relationship(FromKey: callerRec.Key, ToKey: calleeKey, RelType: "INVOKES"));
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
}
