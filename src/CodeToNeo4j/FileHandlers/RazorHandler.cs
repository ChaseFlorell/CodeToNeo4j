using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class RazorHandler(IFileSystem fileSystem) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".razor";

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

        // Extract directives
        ExtractDirectives(content, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);

        // Try to get Roslyn symbols if available (generated code-behind)
        if (document is Document doc && compilation is not null)
        {
            var syntaxTree = await doc.GetSyntaxTreeAsync().ConfigureAwait(false);
            if (syntaxTree != null)
            {
                compilation.GetSemanticModel(syntaxTree);
                // In many cases, the generated C# for Razor can be explored here.
                // For now, we'll focus on the explicit directives as a starting point.
            }
        }
    }

    private static void ExtractDirectives(string content, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility)
        {
            return;
        }

        // Simple regex-based extraction for common Razor directives
        var matches = Regex().Matches(content);

        foreach (Match match in matches)
        {
            var line = match.Value.Trim();
            var kind = line.StartsWith("@using") ? "UsingDirective" :
                line.StartsWith("@inject") ? "InjectDirective" :
                line.StartsWith("@model") ? "ModelDirective" : "InheritsDirective";

            var name = match.Groups[1].Value.Trim();
            var key = $"{fileKey}:{kind}:{name}";
            var startLine = content[..match.Index].Count(c => c == '\n') + 1;

            var record = new Symbol(
                Key: key,
                Name: name,
                Kind: kind,
                Fqn: name,
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

    [GeneratedRegex(@"^@(?:using|inject|model|inherits)\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex Regex();
}