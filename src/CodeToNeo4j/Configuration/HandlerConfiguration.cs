using System.Text.Json.Serialization;

namespace CodeToNeo4j.Configuration;

public record HandlerConfiguration(
	[property: JsonPropertyName("fileExtension")] string FileExtension,
	[property: JsonPropertyName("language")] string Language,
	[property: JsonPropertyName("kindPrefix")] string KindPrefix = "");
