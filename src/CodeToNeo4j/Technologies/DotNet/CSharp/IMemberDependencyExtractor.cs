using CodeToNeo4j.Graph.Models;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Technologies.DotNet.CSharp;

public interface IMemberDependencyExtractor
{
	void ExtractMemberDependencies(
		ISymbol memberSymbol,
		SyntaxNode memberSyntax,
		SemanticModel semanticModel,
		string? repoKey,
		ICollection<Relationship> relBuffer,
		Symbol typeRec,
		Symbol memberRec);

	void AddDependsOnIfExternal(
		ISymbol? symbol,
		IAssemblySymbol currentAssembly,
		string? repoKey,
		string fromKey,
		ICollection<Relationship> relBuffer);
}
