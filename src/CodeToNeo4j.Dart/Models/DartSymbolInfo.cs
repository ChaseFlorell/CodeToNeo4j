using System.Text.Json.Serialization;

namespace CodeToNeo4j.Dart.Models;

public class DartSymbolInfo
{
	[JsonPropertyName("name")]
	public string Name { get; set; } = string.Empty;

	[JsonPropertyName("kind")]
	public string Kind { get; set; } = string.Empty;

	[JsonPropertyName("class")]
	public string Class { get; set; } = string.Empty;

	[JsonPropertyName("fqn")]
	public string Fqn { get; set; } = string.Empty;

	[JsonPropertyName("accessibility")]
	public string Accessibility { get; set; } = "Public";

	[JsonPropertyName("startLine")]
	public int StartLine { get; set; }

	[JsonPropertyName("endLine")]
	public int EndLine { get; set; }

	[JsonPropertyName("documentation")]
	public string? Documentation { get; set; }

	[JsonPropertyName("comments")]
	public string? Comments { get; set; }

	[JsonPropertyName("namespace")]
	public string? Namespace { get; set; }

	[JsonPropertyName("containingClass")]
	public string? ContainingClass { get; set; }
}
