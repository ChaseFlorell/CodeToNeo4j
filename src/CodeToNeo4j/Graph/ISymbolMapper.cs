using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Graph;

public interface ISymbolMapper
{
    Symbol ToSymbolRecord(string repoKey, string fileKey, string filePath, ISymbol symbol, SyntaxNode node);
    string BuildStableSymbolKey(string repoKey, ISymbol symbol);
}