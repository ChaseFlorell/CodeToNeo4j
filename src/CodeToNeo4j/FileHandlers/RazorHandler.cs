using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class RazorHandler : DocumentHandlerBase
{
    public override string FileExtension => ".razor";

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

        // Extract directives
        ExtractDirectives(content, repoKey, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);

        // Try to get Roslyn symbols if available (generated code-behind)
        var doc = document as Document;
        if (doc is not null && compilation is not null)
        {
            var syntaxTree = await doc.GetSyntaxTreeAsync();
            if (syntaxTree != null)
            {
                var semanticModel = compilation.GetSemanticModel(syntaxTree);
                // In many cases, the generated C# for Razor can be explored here.
                // For now, we'll focus on the explicit directives as a starting point.
            }
        }
    }

    private void ExtractDirectives(string content, string repoKey, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Simple regex-based extraction for common Razor directives
        var matches = Regex.Matches(content, @"^@(?:using|inject|model|inherits)\s+(.+)$", RegexOptions.Multiline);

        foreach (Match match in matches)
        {
            var line = match.Value.Trim();
            var kind = line.StartsWith("@using") ? "UsingDirective" :
                       line.StartsWith("@inject") ? "InjectDirective" :
                       line.StartsWith("@model") ? "ModelDirective" : "InheritsDirective";
            
            var name = match.Groups[1].Value.Trim();
            var key = $"{fileKey}:{kind}:{name}";
            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;

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
}
