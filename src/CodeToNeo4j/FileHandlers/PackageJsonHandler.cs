using System.IO.Abstractions;
using System.Text.Json;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class PackageJsonHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, ILogger<PackageJsonHandler> logger)
    : PackageDependencyHandlerBase(fileSystem, textSymbolMapper)
{
    public override string FileExtension => "package.json";

    public override bool CanHandle(string filePath)
        => Path.GetFileName(filePath).Equals("package.json", StringComparison.OrdinalIgnoreCase);

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
            using var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;

            ExtractDependencySection(root, "dependencies", fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
            ExtractDependencySection(root, "devDependencies", fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
        }
        catch (JsonException)
        {
            logger.LogWarning("Failed to parse package.json: {FilePath}", filePath);
        }

        return new FileResult(fileNamespace, fileKey);
    }

    private void ExtractDependencySection(
        JsonElement root,
        string sectionName,
        string fileKey,
        string relativePath,
        string? fileNamespace,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer)
    {
        if (!root.TryGetProperty(sectionName, out var section) || section.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in section.EnumerateObject())
        {
            var name = prop.Name;
            var version = prop.Value.GetString();

            if (string.IsNullOrEmpty(name)) continue;

            AddDependency(name, version, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
        }
    }
}
