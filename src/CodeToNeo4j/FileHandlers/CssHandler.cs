using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class CssHandler : DocumentHandlerBase
{
    public override string FileExtension => ".css";

    public override async ValueTask HandleAsync(
        TextDocument? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        string databaseName,
        Accessibility minAccessibility)
    {
        await base.HandleAsync(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
        string content = await GetContent(document, filePath);

        ExtractSelectors(content, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
    }

    private void ExtractSelectors(string content, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Basic regex to find CSS selectors
        var selectorRegex = new Regex(@"([^{]+)\s*\{", RegexOptions.Multiline);
        var matches = selectorRegex.Matches(content);

        foreach (Match match in matches)
        {
            var selector = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(selector) || selector.StartsWith("@")) continue;

            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = $"{fileKey}:CssSelector:{selector}:{startLine}";

            var record = new Symbol(
                Key: key,
                Name: selector,
                Kind: "CssSelector",
                Fqn: selector,
                Accessibility: "Public",
                FileKey: fileKey,
                FilePath: filePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }
}
