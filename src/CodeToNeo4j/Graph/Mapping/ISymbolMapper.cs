using CodeToNeo4j.Graph.Models;
using Microsoft.CodeAnalysis;
namespace CodeToNeo4j.Graph.Mapping;

public interface ISymbolMapper
{
	Symbol ToSymbolRecord(string? repoKey, string fileKey, string relativePath, string? fileNamespace, ISymbol symbol, SyntaxNode node, string language = "unknown", string technology = "unknown");
	string BuildStableSymbolKey(string? repoKey, ISymbol symbol);
}
