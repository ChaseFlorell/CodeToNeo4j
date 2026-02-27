using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CodeToNeo4j;

public interface ISymbolMapper
{
    SymbolRecord ToSymbolRecord(string repoKey, string fileKey, string filePath, ISymbol symbol, SyntaxNode node);
    string BuildStableSymbolKey(string repoKey, ISymbol symbol);
}

public class SymbolMapper : ISymbolMapper
{
    public SymbolRecord ToSymbolRecord(string repoKey, string fileKey, string filePath, ISymbol symbol, SyntaxNode node)
    {
        var kind = symbol.Kind.ToString();
        var name = symbol.Name;
        var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var key = BuildStableSymbolKey(repoKey, symbol);
        var (startLine, endLine) = GetLineSpan(node.GetLocation());
        var documentation = symbol.GetDocumentationCommentXml();
        var comments = ExtractComments(node);

        return new SymbolRecord(
            Key: key,
            Name: name,
            Kind: kind,
            Fqn: fqn,
            Accessibility: symbol.DeclaredAccessibility.ToString(),
            FileKey: fileKey,
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine,
            Documentation: string.IsNullOrWhiteSpace(documentation) ? null : documentation,
            Comments: string.IsNullOrWhiteSpace(comments) ? null : comments
        );
    }

    public string BuildStableSymbolKey(string repoKey, ISymbol symbol)
    {
        var display = symbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return $"{repoKey}:{display}";
    }

    private static (int startLine, int endLine) GetLineSpan(Location loc)
    {
        if (!loc.IsInSource) return (-1, -1);
        var span = loc.GetLineSpan();
        return (span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
    }

    private static string? ExtractComments(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia();
        var comments = trivia
            .Where(t => t.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                        t.IsKind(SyntaxKind.MultiLineCommentTrivia))
            .Select(t => t.ToString().Trim())
            .ToList();

        return comments.Count > 0 ? string.Join(Environment.NewLine, comments) : null;
    }
}
