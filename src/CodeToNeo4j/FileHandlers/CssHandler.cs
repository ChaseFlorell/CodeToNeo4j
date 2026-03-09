using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class CssHandler (IFileSystem fileSystem) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".css";

    protected override async Task HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        var content = await GetContent(document, filePath).ConfigureAwait(false);

        CssHandler.ExtractSelectors(content, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
    }

    private static void ExtractSelectors(string content, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Basic regex to find CSS selectors
        var matches = Regex().Matches(content);

        foreach (Match match in matches)
        {
            var selector = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(selector) || selector.StartsWith("@")) continue;

            var startLine = content[..match.Index].Count(c => c == '\n') + 1;
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

    [GeneratedRegex(@"([^{]+)\s*\{", RegexOptions.Multiline)]
    private static partial Regex Regex();
}
