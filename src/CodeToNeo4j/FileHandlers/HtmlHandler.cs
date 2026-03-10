using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class HtmlHandler(IFileSystem fileSystem) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".html";

    protected override async Task<string?> HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        string relativePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        var content = await GetContent(document, filePath).ConfigureAwait(false);
        var fileNamespace = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

        // Extract script references
        ExtractScriptReferences(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

        // Extract IDs and Classes
        HtmlHandler.ExtractIdsAndClasses(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

        return fileNamespace;
    }

    private static void ExtractScriptReferences(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility)
        {
            return;
        }

        var scriptRegex = ScriptRegex();
        foreach (Match match in scriptRegex.Matches(content))
        {
            var src = match.Groups[1].Value;
            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = $"{fileKey}:ScriptRef:{src}:{startLine}";

            var record = new Symbol(
                Key: key,
                Name: src,
                Kind: "HtmlScriptReference",
                Fqn: src,
                Accessibility: "Public",
                FileKey: fileKey,
                RelativePath: relativePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null,
                Namespace: fileNamespace
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
        }
    }

    private static void ExtractIdsAndClasses(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Extract IDs
        var idRegex = new Regex(@"id=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        foreach (Match match in idRegex.Matches(content))
        {
            var id = match.Groups[1].Value;
            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = $"{fileKey}:ElementId:{id}:{startLine}";

            var record = new Symbol(
                Key: key,
                Name: id,
                Kind: "HtmlElementId",
                Fqn: id,
                Accessibility: "Public",
                FileKey: fileKey,
                RelativePath: relativePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null,
                Namespace: fileNamespace
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }

    [GeneratedRegex(@"<script\s+.*?src=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-CA")]
    private static partial Regex ScriptRegex();
}