using System.IO.Abstractions;
using System.Text.Json;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class JsonHandler(
	IFileSystem fileSystem,
	ILogger<JsonHandler> logger,
	ITextSymbolMapper textSymbolMapper) : DocumentHandlerBase(fileSystem)
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
		var fileNamespace = _fileSystem.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

		try
		{
			using JsonDocument jsonDoc = JsonDocument.Parse(content);
			ProcessElement(jsonDoc.RootElement, fileKey, relativePath, fileNamespace ?? string.Empty, symbolBuffer, relBuffer, minAccessibility, "");
		}
		catch (JsonException)
		{
			logger.LogWarning("Failed to parse JSON file: {FilePath}", filePath);
		}

		return new(fileNamespace, fileKey);
	}

	private void ProcessElement(JsonElement element, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, Accessibility minAccessibility, string path)
	{
		if (!IsPublicAccessible(minAccessibility))
		{
			return;
		}

		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				foreach (var property in element.EnumerateObject())
				{
					var propertyPath = string.IsNullOrEmpty(path) ? property.Name : $"{path}.{property.Name}";
					var key = textSymbolMapper.BuildKey(fileKey, "JsonProperty", propertyPath);

					var record = textSymbolMapper.CreateSymbol(
						key,
						property.Name,
						"JsonProperty",
						"property",
						propertyPath,
						fileKey,
						relativePath,
						fileNamespace,
						-1); // System.Text.Json.JsonDocument does not provide line numbers easily

					symbolBuffer.Add(record);
					relBuffer.Add(new(fileKey, key, "CONTAINS"));

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

	private readonly IFileSystem _fileSystem = fileSystem;
}
