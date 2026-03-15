using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class HtmlHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".html";

    protected override async Task<FileResult> HandleFile(
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
        ExtractIdsAndClasses(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

        return new FileResult(fileNamespace, fileKey);
    }

    private void ExtractScriptReferences(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
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
            var key = textSymbolMapper.BuildKey(fileKey, "ScriptRef", src, startLine);

            var record = textSymbolMapper.CreateSymbol(
                key: key,
                name: src,
                kind: "HtmlScriptReference",
                @class: "script",
                fqn: src,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine);

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
        }
    }

    private void ExtractIdsAndClasses(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        // Extract IDs
        var idRegex = IdRegex();
        foreach (Match match in idRegex.Matches(content))
        {
            var id = match.Groups[1].Value;
            var startLine = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            var key = textSymbolMapper.BuildKey(fileKey, "ElementId", id, startLine);

            var record = textSymbolMapper.CreateSymbol(
                key: key,
                name: id,
                kind: "HtmlElementId",
                @class: "element",
                fqn: id,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine);

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }

    [GeneratedRegex(@"id=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-CA")]
    private static partial Regex IdRegex();

    [GeneratedRegex(@"<script\s+.*?src=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-CA")]
    private static partial Regex ScriptRegex();
}
