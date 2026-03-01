using System.Text.Json;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class JsonHandler : DocumentHandlerBase
{
    public override string FileExtension => ".json";

    public override async ValueTask HandleAsync(
        TextDocument? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        string databaseName,
        Accessibility minAccessibility)
    {
        await base.HandleAsync(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
        string content = await GetContent(document, filePath);

        try
        {
            using var jsonDoc = JsonDocument.Parse(content);
            ProcessElement(jsonDoc.RootElement, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility, "");
        }
        catch (JsonException)
        {
            // Fail gracefully for malformed JSON
        }
    }

    private void ProcessElement(JsonElement element, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility, string path)
    {
        if (Accessibility.Public < minAccessibility) return;

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
                        Fqn: propertyPath,
                        Accessibility: "Public",
                        FileKey: fileKey,
                        FilePath: filePath,
                        StartLine: -1, // System.Text.Json.JsonDocument does not provide line numbers easily
                        EndLine: -1,
                        Documentation: null,
                        Comments: null
                    );

                    symbolBuffer.Add(record);
                    relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));

                    ProcessElement(property.Value, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility, propertyPath);
                }
                break;
            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var itemPath = $"{path}[{index}]";
                    ProcessElement(item, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility, itemPath);
                    index++;
                }
                break;
        }
    }
}
