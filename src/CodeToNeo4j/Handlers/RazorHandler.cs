using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using CodeToNeo4j.Neo4j;

namespace CodeToNeo4j.Handlers;

public class RazorHandler : IDocumentHandler
{
    public bool CanHandle(string filePath) => filePath.EndsWith(".razor", StringComparison.OrdinalIgnoreCase);

    public async ValueTask HandleAsync(
        Document document,
        Compilation compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
        string databaseName)
    {
        var sourceText = await document.GetTextAsync();
        var content = sourceText.ToString();

        // Extract directives
        ExtractDirectives(content, repoKey, fileKey, filePath, symbolBuffer, relBuffer);

        // Try to get Roslyn symbols if available (generated code-behind)
        var syntaxTree = await document.GetSyntaxTreeAsync();
        if (syntaxTree != null)
        {
            var semanticModel = compilation.GetSemanticModel(syntaxTree);
            // In many cases, the generated C# for Razor can be explored here.
            // For now, we'll focus on the explicit directives as a starting point.
        }
    }

    private void ExtractDirectives(string content, string repoKey, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer)
    {
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

            var record = new SymbolRecord(
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
            relBuffer.Add(new RelRecord(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }
}
