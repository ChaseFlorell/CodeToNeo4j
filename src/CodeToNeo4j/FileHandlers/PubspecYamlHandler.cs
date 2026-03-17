using System.IO.Abstractions;
using CodeToNeo4j.Dart.Yaml;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class PubspecYamlHandler(
    IFileSystem fileSystem,
    ITextSymbolMapper textSymbolMapper,
    ILogger<PubspecYamlHandler> logger) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => "pubspec.yaml";

    public override bool CanHandle(string filePath)
        => Path.GetFileName(filePath).Equals("pubspec.yaml", StringComparison.OrdinalIgnoreCase);

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
        var fileNamespace = Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

        if (Accessibility.Public < minAccessibility)
            return new FileResult(fileNamespace, fileKey);

        var content = await GetContent(document, filePath).ConfigureAwait(false);

        try
        {
            var pubspec = PubspecParser.Parse(content);

            foreach (var dep in pubspec.Dependencies)
            {
                AddDependency(dep.Name, dep.Version, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
            }

            foreach (var dep in pubspec.DevDependencies)
            {
                AddDependency(dep.Name, dep.Version, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse pubspec.yaml: {FilePath}", filePath);
        }

        return new FileResult(fileNamespace, fileKey);
    }

    private void AddDependency(
        string name,
        string? version,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer)
    {
        var key = $"pkg:{name}";
        var symbol = textSymbolMapper.CreateSymbol(
            key: key,
            name: name,
            kind: "Dependency",
            @class: name,
            fqn: version is not null ? $"{name} ({version})" : name,
            fileKey: fileKey,
            relativePath: relativePath,
            fileNamespace: fileNamespace,
            startLine: -1,
            documentation: version,
            version: version);

        symbolBuffer.Add(symbol);
        relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "DEPENDS_ON"));
    }
}
