using System.Text.Json.Serialization;

namespace CodeToNeo4j.Dart.Models;

public class DartFileResult
{
	[JsonPropertyName("symbols")]
	public List<DartSymbolInfo> Symbols { get; set; } = [];

	[JsonPropertyName("relationships")]
	public List<DartRelationshipInfo> Relationships { get; set; } = [];
}
