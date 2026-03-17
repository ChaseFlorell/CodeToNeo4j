using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class CssHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".css";

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
        var fileNamespace = _fileSystem.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

        ExtractSelectors(content, fileKey, relativePath, fileNamespace ?? string.Empty, symbolBuffer, relBuffer, minAccessibility);

        return new FileResult(fileNamespace, fileKey);
    }

    private void ExtractSelectors(string content, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (!IsPublicAccessible(minAccessibility)) return;

        // Basic regex to find CSS selectors
        var matches = Regex().Matches(content);

        foreach (Match match in matches)
        {
            var selector = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(selector) || selector.StartsWith("@")) continue;

            var startLine = GetLineNumber(content, match.Index);
            var key = textSymbolMapper.BuildKey(fileKey, "CssSelector", selector, startLine);

            var record = textSymbolMapper.CreateSymbol(
                key: key,
                name: selector,
                kind: "CssSelector",
                @class: "selector",
                fqn: selector,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine);

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }

    [GeneratedRegex(@"([^{]+)\s*\{", RegexOptions.Multiline)]
    private static partial Regex Regex();

    private readonly IFileSystem _fileSystem = fileSystem;
}
