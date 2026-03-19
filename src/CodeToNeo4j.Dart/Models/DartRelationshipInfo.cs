using System.Text.Json.Serialization;

namespace CodeToNeo4j.Dart.Models;

public class DartRelationshipInfo
{
	[JsonPropertyName("fromSymbol")]
	public string FromSymbol { get; set; } = string.Empty;

	[JsonPropertyName("fromKind")]
	public string FromKind { get; set; } = string.Empty;

	[JsonPropertyName("fromLine")]
	public int FromLine { get; set; }

	[JsonPropertyName("toSymbol")]
	public string ToSymbol { get; set; } = string.Empty;

	[JsonPropertyName("toKind")]
	public string ToKind { get; set; } = string.Empty;

	[JsonPropertyName("toLine")]
	public int? ToLine { get; set; }

	[JsonPropertyName("toFile")]
	public string? ToFile { get; set; }

	[JsonPropertyName("relType")]
	public string RelType { get; set; } = string.Empty;
}
