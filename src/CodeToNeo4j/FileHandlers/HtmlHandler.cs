using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class HtmlHandler : DocumentHandlerBase
{
    public override string FileType => "HTML";
    public override bool CanHandle(string filePath) => filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

    public override async ValueTask HandleAsync(
        Document? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
        string databaseName,
        Accessibility minAccessibility)
    {
        await base.HandleAsync(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
        string content = await GetContent(document, filePath);

        // Extract script references
        ExtractScriptReferences(content, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        
        // Extract IDs and Classes
        ExtractIdsAndClasses(content, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
    }

    private void ExtractScriptReferences(string content, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        var scriptRegex = new Regex(@"<script\s+.*?src=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        foreach (Match match in scriptRegex.Matches(content))
        {
            var src = match.Groups[1].Value;
            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = $"{fileKey}:ScriptRef:{src}:{startLine}";

            var record = new SymbolRecord(
                Key: key,
                Name: src,
                Kind: "HtmlScriptReference",
                Fqn: src,
                Accessibility: "Public",
                FileKey: fileKey,
                FilePath: filePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new RelRecord(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
        }
    }

    private void ExtractIdsAndClasses(string content, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Extract IDs
        var idRegex = new Regex(@"id=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        foreach (Match match in idRegex.Matches(content))
        {
            var id = match.Groups[1].Value;
            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = $"{fileKey}:ElementId:{id}:{startLine}";

            var record = new SymbolRecord(
                Key: key,
                Name: id,
                Kind: "HtmlElementId",
                Fqn: id,
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
