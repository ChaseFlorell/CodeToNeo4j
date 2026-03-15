using System.IO.Abstractions;
using System.Text.Json;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class JsonHandler(
    IFileSystem fileSystem,
    ILogger<JsonHandler> logger) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".json";

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

        try
        {
            using var jsonDoc = JsonDocument.Parse(content);
            ProcessElement(jsonDoc.RootElement, fileKey, relativePath, fileNamespace ?? string.Empty, symbolBuffer, relBuffer, minAccessibility, "");
        }
        catch (JsonException)
        {
            logger.LogWarning("Failed to parse JSON file: {FilePath}", filePath);
        }

        return new FileResult(fileNamespace, fileKey);
    }

    private static void ProcessElement(JsonElement element, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility, string path)
    {
        if (Accessibility.Public < minAccessibility)
        {
            return;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
                    var key = $"{fileKey}:JsonProperty:{propertyPath}";

                    var record = new Symbol(
                        Key: key,
                        Name: property.Name,
                        Kind: "JsonProperty",
                        Class: "property",
                        Fqn: propertyPath,
                        Accessibility: "Public",
                        FileKey: fileKey,
                        RelativePath: relativePath,
                        StartLine: -1, // System.Text.Json.JsonDocument does not provide line numbers easily
                        EndLine: -1,
                        Documentation: null,
                        Comments: null,
                        Namespace: fileNamespace,
                        Version: null
                    );

                    symbolBuffer.Add(record);
                    relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));

                    ProcessElement(property.Value, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility, propertyPath);
                }

                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var itemPath = $"{path}[{index}]";
                    ProcessElement(item, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility, itemPath);
                    index++;
                }

                break;
        }
    }
}