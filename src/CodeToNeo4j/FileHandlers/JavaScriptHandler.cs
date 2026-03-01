using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class JavaScriptHandler : DocumentHandlerBase
{
    public override string FileType => "JavaScript";
    public override bool CanHandle(string filePath) => filePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase);

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

        // Extract function definitions (basic)
        ExtractFunctions(content, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        
        // Extract imports/exports (basic)
        ExtractImportsExports(content, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
    }

    private void ExtractFunctions(string content, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Regex for: function name(...) or const name = (...) => or name: function(...)
        var functionRegex = new Regex(@"(?:function\s+([a-zA-Z0-9_$]+)|(?:const|let|var)\s+([a-zA-Z0-9_$]+)\s*=\s*(?:async\s*)?\(.*?\)\s*=>|([a-zA-Z0-9_$]+)\s*:\s*function)", RegexOptions.Multiline);
        var matches = functionRegex.Matches(content);

        foreach (Match match in matches)
        {
            var name = match.Groups[1].Value;
            if (string.IsNullOrEmpty(name)) name = match.Groups[2].Value;
            if (string.IsNullOrEmpty(name)) name = match.Groups[3].Value;

            if (string.IsNullOrEmpty(name)) continue;

            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = $"{fileKey}:Function:{name}:{startLine}";

            var record = new SymbolRecord(
                Key: key,
                Name: name,
                Kind: "JavaScriptFunction",
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

    private void ExtractImportsExports(string content, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Extract imports
        var importRegex = new Regex(@"import\s+.*?\s+from\s+['""](.*?)['""]", RegexOptions.Multiline);
        foreach (Match match in importRegex.Matches(content))
        {
            var module = match.Groups[1].Value;
            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = $"{fileKey}:Import:{module}:{startLine}";

            var record = new SymbolRecord(
                Key: key,
                Name: module,
                Kind: "JavaScriptImport",
                Fqn: module,
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
}
