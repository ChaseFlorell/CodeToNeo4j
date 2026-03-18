using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class TypeScriptHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper) : JsHandlerBase(fileSystem, textSymbolMapper)
{
    public override string FileExtension => ".ts";
    protected override string KindPrefix => "TypeScript";

    public override bool CanHandle(string filePath)
        => filePath.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
           || filePath.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase);

    protected override void ExtractAdditionalSymbols(
        string content,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        if (!IsPublicAccessible(minAccessibility))
        {
            return;
        }

        ExtractInterfaces(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
        ExtractTypeAliases(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
        ExtractEnums(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
    }

    private void ExtractInterfaces(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer)
    {
        foreach (Match match in InterfaceRegex().Matches(content))
        {
            var name = match.Groups[1].Value;
            var startLine = GetLineNumber(content, match.Index);
            var key = TextSymbolMapper.BuildKey(fileKey, "Interface", name, startLine);

            symbolBuffer.Add(TextSymbolMapper.CreateSymbol(
                key: key,
                name: name,
                kind: "TypeScriptInterface",
                @class: "interface",
                fqn: name,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine));

            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }

    private void ExtractTypeAliases(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer)
    {
        foreach (Match match in TypeAliasRegex().Matches(content))
        {
            var name = match.Groups[1].Value;
            var startLine = GetLineNumber(content, match.Index);
            var key = TextSymbolMapper.BuildKey(fileKey, "TypeAlias", name, startLine);

            symbolBuffer.Add(TextSymbolMapper.CreateSymbol(
                key: key,
                name: name,
                kind: "TypeScriptTypeAlias",
                @class: "type",
                fqn: name,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine));

            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }

    private void ExtractEnums(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer)
    {
        foreach (Match match in EnumRegex().Matches(content))
        {
            var name = match.Groups[1].Value;
            var startLine = GetLineNumber(content, match.Index);
            var key = TextSymbolMapper.BuildKey(fileKey, "Enum", name, startLine);

            symbolBuffer.Add(TextSymbolMapper.CreateSymbol(
                key: key,
                name: name,
                kind: "TypeScriptEnum",
                @class: "enum",
                fqn: name,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: fileNamespace,
                startLine: startLine));

            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));
        }
    }

    [GeneratedRegex(@"(?:^|\s)interface\s+([a-zA-Z0-9_$]+)", RegexOptions.Multiline)]
    private static partial Regex InterfaceRegex();
    [GeneratedRegex(@"(?:^|\s)type\s+([a-zA-Z0-9_$]+)(?:<[^>]*>)?\s*=", RegexOptions.Multiline)]
    private static partial Regex TypeAliasRegex();
    [GeneratedRegex(@"(?:^|\s)(?:const\s+)?enum\s+([a-zA-Z0-9_$]+)", RegexOptions.Multiline)]
    private static partial Regex EnumRegex();
}
