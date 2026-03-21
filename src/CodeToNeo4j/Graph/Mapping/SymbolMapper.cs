using CodeToNeo4j.Graph.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
namespace CodeToNeo4j.Graph.Mapping;

public class SymbolMapper : ISymbolMapper
{
	public Symbol ToSymbolRecord(string? repoKey, string fileKey, string relativePath, string? fileNamespace, ISymbol symbol, SyntaxNode node, string language = "unknown", string technology = "unknown")
	{
		var kind = symbol.Kind.ToString();
		var name = symbol.Name;
		var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
		var className = symbol.Name;
		var key = BuildStableSymbolKey(repoKey, symbol);
		var (startLine, endLine) = GetLineSpan(node.GetLocation());
		var documentation = symbol.GetDocumentationCommentXml();
		var comments = ExtractComments(node);

		var @namespace = symbol is INamedTypeSymbol nts
			? nts.ContainingNamespace.ToDisplayString()
			: fileNamespace;

		return new(
			key,
			name,
			kind,
			className,
			fqn,
			symbol.DeclaredAccessibility.ToString(),
			fileKey,
			relativePath,
			startLine,
			endLine,
			string.IsNullOrWhiteSpace(documentation) ? null : documentation,
			string.IsNullOrWhiteSpace(comments) ? null : comments,
			@namespace,
			null,
			language,
			technology
		);
	}

	public string BuildStableSymbolKey(string? repoKey, ISymbol symbol)
	{
		var display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
		return $"{repoKey}:{display}";
	}

	private static (int startLine, int endLine) GetLineSpan(Location loc)
	{
		if (!loc.IsInSource)
		{
			return (-1, -1);
		}

		var span = loc.GetMappedLineSpan();
		if (!span.IsValid)
		{
			span = loc.GetLineSpan();
		}

		return (span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
	}

	private static string? ExtractComments(SyntaxNode node)
	{
		var trivia = node.GetLeadingTrivia();
		var comments = trivia
			.Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
						t.IsKind(SyntaxKind.MultiLineCommentTrivia))
			.Select(t => t.ToString().Trim())
			.ToArray();

		return comments.Length > 0
			? string.Join(Environment.NewLine, comments)
			: null;
	}
}
