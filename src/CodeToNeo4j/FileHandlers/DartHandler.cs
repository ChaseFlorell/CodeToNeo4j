using System.IO.Abstractions;
using CodeToNeo4j.Dart.Bridge;
using CodeToNeo4j.Dart.Models;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class DartHandler(
    IFileSystem fileSystem,
    ITextSymbolMapper textSymbolMapper,
    IDartBridgeService dartBridgeService,
    ILogger<DartHandler> logger) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".dart";

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
        var fileNamespace = _fileSystem.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

        // Find the project root by walking up to find pubspec.yaml
        var projectRoot = FindProjectRoot(filePath, _fileSystem);
        if (projectRoot is null)
        {
            logger.LogDebug("No pubspec.yaml found for {FilePath}, skipping Dart analysis", filePath);
            return new FileResult(fileNamespace, fileKey);
        }

        var analysisResult = await dartBridgeService.AnalyzeProject(projectRoot).ConfigureAwait(false);
        if (analysisResult is null)
            return new FileResult(fileNamespace, fileKey);

        // Find this file's results in the analysis
        var normalizedRelativePath = relativePath.Replace('\\', '/');
        DartFileResult? fileResult = null;

        foreach (var (key, value) in analysisResult.Files)
        {
            if (key.Replace('\\', '/').Equals(normalizedRelativePath, StringComparison.OrdinalIgnoreCase))
            {
                fileResult = value;
                break;
            }
        }

        if (fileResult is null)
        {
            logger.LogDebug("No analysis results found for {FilePath}", filePath);
            return new FileResult(fileNamespace, fileKey);
        }

        foreach (var symbolInfo in fileResult.Symbols)
        {
            if (!ShouldInclude(symbolInfo.Accessibility, minAccessibility))
                continue;

            var symbolKey = textSymbolMapper.BuildKey(fileKey, symbolInfo.Kind, symbolInfo.Name, symbolInfo.StartLine);
            var symbol = textSymbolMapper.CreateSymbol(
                key: symbolKey,
                name: symbolInfo.Name,
                kind: symbolInfo.Kind,
                @class: symbolInfo.Class,
                fqn: symbolInfo.Fqn,
                fileKey: fileKey,
                relativePath: relativePath,
                fileNamespace: symbolInfo.Namespace ?? fileNamespace,
                startLine: symbolInfo.StartLine,
                accessibility: symbolInfo.Accessibility,
                documentation: symbolInfo.Documentation);

            symbolBuffer.Add(symbol);
        }

        foreach (var rel in fileResult.Relationships)
        {
            var fromKey = textSymbolMapper.BuildKey(fileKey, rel.FromKind, rel.FromSymbol, rel.FromLine);
            var toKey = textSymbolMapper.BuildKey(fileKey, rel.ToKind, rel.ToSymbol, rel.ToLine);

            relBuffer.Add(new Relationship(FromKey: fromKey, ToKey: toKey, RelType: rel.RelType));
        }

        return new FileResult(fileNamespace, fileKey);
    }

    private readonly IFileSystem _fileSystem = fileSystem;

    private static string? FindProjectRoot(string filePath, IFileSystem fs)
    {
        var dir = fs.Path.GetDirectoryName(filePath);
        while (!string.IsNullOrEmpty(dir))
        {
            if (fs.File.Exists(fs.Path.Combine(dir, "pubspec.yaml")))
                return dir;
            dir = fs.Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static bool ShouldInclude(string accessibility, Accessibility minAccessibility)
    {
        if (minAccessibility == Accessibility.NotApplicable)
            return true;

        var mapped = accessibility switch
        {
            "Public" => Accessibility.Public,
            "Private" => Accessibility.Private,
            "Protected" => Accessibility.Protected,
            "Internal" => Accessibility.Internal,
            _ => Accessibility.Public,
        };

        return mapped >= minAccessibility;
    }
}
