using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CodeToNeo4j.Technologies.DotNet.CSharp;

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
		if (symbol == null)
		{
			return;
		}

		if (symbol is INamespaceSymbol nsSymbol)
		{
			if (nsSymbol.ContainingAssembly != null && !SymbolEqualityComparer.Default.Equals(nsSymbol.ContainingAssembly, currentAssembly))
			{
				var depKey = $"{repoKey}:{nsSymbol.ToDisplayString()}";
				if (!relBuffer.Any(r => r.FromKey == fromKey && r.ToKey == depKey && r.RelType == GraphSchema.Relationships.DependsOn))
				{
					relBuffer.Add(new Relationship(fromKey, depKey, GraphSchema.Relationships.DependsOn));
				}
			}
		}
		else if (symbol is INamedTypeSymbol typeSymbol)
		{
			if (typeSymbol.ContainingAssembly != null && !SymbolEqualityComparer.Default.Equals(typeSymbol.ContainingAssembly, currentAssembly))
			{
				var depKey = $"{repoKey}:{typeSymbol.ToDisplayString()}";
				if (!relBuffer.Any(r => r.FromKey == fromKey && r.ToKey == depKey && r.RelType == GraphSchema.Relationships.DependsOn))
				{
					relBuffer.Add(new Relationship(fromKey, depKey, GraphSchema.Relationships.DependsOn));
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
		if (body == null)
		{
			return;
		}

		HashSet<string> seenCallees = [];

		foreach (var node in body.DescendantNodes())
		{
			switch (node)
			{
				// Method invocations
				case InvocationExpressionSyntax invocation:
					if (semanticModel.GetSymbolInfo(invocation).Symbol is IMethodSymbol calleeSymbol)
					{
						AddInvokes(calleeSymbol, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;

				// Constructor invocations
				case BaseObjectCreationExpressionSyntax objectCreation:
					if (semanticModel.GetSymbolInfo(objectCreation).Symbol is IMethodSymbol ctorSymbol)
					{
						AddInvokes(ctorSymbol, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;

				// Binary operators (==, !=, >, <, >=, <=, |, &, ^) and 'as' expressions
				case BinaryExpressionSyntax binary:
					if (semanticModel.GetSymbolInfo(binary).Symbol is IMethodSymbol
					    {
						    MethodKind: MethodKind.UserDefinedOperator or MethodKind.Conversion
					    } binSymbol)
					{
						AddInvokes(binSymbol, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;

				// Explicit cast expressions: (int)foo
				case CastExpressionSyntax cast:
					if (semanticModel.GetSymbolInfo(cast).Symbol is IMethodSymbol { MethodKind: MethodKind.Conversion } convSymbol)
					{
						AddInvokes(convSymbol, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;

				// Unary prefix operators: !, ~, +, -, ++, --
				case PrefixUnaryExpressionSyntax prefix:
					if (semanticModel.GetSymbolInfo(prefix).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } prefixOp)
					{
						AddInvokes(prefixOp, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;

				// Unary postfix operators: ++, --
				case PostfixUnaryExpressionSyntax postfix:
					if (semanticModel.GetSymbolInfo(postfix).Symbol is IMethodSymbol { MethodKind: MethodKind.UserDefinedOperator } postfixOp)
					{
						AddInvokes(postfixOp, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;

				// Method groups: identifiers or member access expressions that resolve to
				// an IMethodSymbol but are NOT the expression being invoked in an InvocationExpression.
				case IdentifierNameSyntax id when !IsInvocationTarget(id):
					if (semanticModel.GetSymbolInfo(id).Symbol is IMethodSymbol idMethodGroup)
					{
						AddInvokes(idMethodGroup, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;

				case MemberAccessExpressionSyntax memberAccess when !IsInvocationTarget(memberAccess):
					if (semanticModel.GetSymbolInfo(memberAccess).Symbol is IMethodSymbol maMethodGroup)
					{
						AddInvokes(maMethodGroup, callerRec, repoKey, relBuffer, seenCallees);
					}

					break;
			}

			// Implicit conversions (assignments, arguments, return values, etc.)
			if (node is ExpressionSyntax expr && IsImplicitConversionCandidate(expr))
			{
				var conversion = semanticModel.GetConversion(expr);
				if (conversion.IsUserDefined && conversion.MethodSymbol is { MethodKind: MethodKind.Conversion } implicitSymbol)
				{
					AddInvokes(implicitSymbol, callerRec, repoKey, relBuffer, seenCallees);
				}
			}
		}
	}

	private static bool IsInvocationTarget(SyntaxNode node)
	{
		// Direct invocation target: node is the Expression of an InvocationExpressionSyntax
		if (node.Parent is InvocationExpressionSyntax invocation && invocation.Expression == node)
		{
			return true;
		}

		// Identifier is the Name part of a MemberAccessExpression that is itself an invocation target
		// e.g. `this.DoWork()` — `DoWork` is IdentifierNameSyntax, parent is MemberAccessExpressionSyntax
		if (node is IdentifierNameSyntax
		    && node.Parent is MemberAccessExpressionSyntax parentMember
		    && parentMember.Parent is InvocationExpressionSyntax parentInvocation
		    && parentInvocation.Expression == parentMember)
		{
			return true;
		}

		return false;
	}

	private static bool IsImplicitConversionCandidate(ExpressionSyntax expr)
	{
		return expr.Parent switch
		{
			EqualsValueClauseSyntax => true,
			AssignmentExpressionSyntax a => expr == a.Right,
			ArgumentSyntax => true,
			ReturnStatementSyntax => true,
			ArrowExpressionClauseSyntax => true,
			_ => false
		};
	}

	private void AddInvokes(
		IMethodSymbol calleeSymbol,
		Symbol callerRec,
		string? repoKey,
		ICollection<Relationship> relBuffer,
		HashSet<string> seenCallees)
	{
		var calleeKey = symbolMapper.BuildStableSymbolKey(repoKey, calleeSymbol);
		if (seenCallees.Add(calleeKey))
		{
			relBuffer.Add(new(callerRec.Key, calleeKey, GraphSchema.Relationships.Invokes));
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
			IParameterSymbol? parameterSymbol = semanticModel.GetDeclaredSymbol(parameter);
			if (parameterSymbol?.Type is not (null or IErrorTypeSymbol))
			{
				AddDependsOnRelationship(typeRec.Key, parameterSymbol.Type, repoKey, relBuffer);
			}
		}

		if (semanticModel.GetDeclaredSymbol(bmds) is { ReturnType: not null and not IErrorTypeSymbol } baseMethodSymbol
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
		relBuffer.Add(new(fromKey, depKey, GraphSchema.Relationships.DependsOn));
	}
}
