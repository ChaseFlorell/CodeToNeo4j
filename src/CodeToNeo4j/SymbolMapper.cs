using Microsoft.CodeAnalysis;

namespace CodeToNeo4j;

public interface ISymbolMapper
{
    SymbolRecord ToSymbolRecord(string repoKey, string fileKey, string filePath, ISymbol symbol, Location loc);
    string BuildStableSymbolKey(string repoKey, ISymbol symbol);
}

public class SymbolMapper : ISymbolMapper
{
    public SymbolRecord ToSymbolRecord(string repoKey, string fileKey, string filePath, ISymbol symbol, Location loc)
    {
        var kind = symbol.Kind.ToString();
        var name = symbol.Name;
        var fqn = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var key = BuildStableSymbolKey(repoKey, symbol);
        var (startLine, endLine) = GetLineSpan(loc);

        return new SymbolRecord(
            Key: key,
            Name: name,
            Kind: kind,
            Fqn: fqn,
            Accessibility: symbol.DeclaredAccessibility.ToString(),
            FileKey: fileKey,
            FilePath: filePath,
            StartLine: startLine,
            EndLine: endLine
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
}
