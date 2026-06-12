using System.Text.Json.Serialization;

namespace CodeToNeo4j.TypeScript.Models;

public class TsFileResult
{
	[JsonPropertyName("symbols")]
	public List<TsSymbolInfo> Symbols { get; set; } = [];

	[JsonPropertyName("relationships")]
	public List<TsRelationshipInfo> Relationships { get; set; } = [];
}
