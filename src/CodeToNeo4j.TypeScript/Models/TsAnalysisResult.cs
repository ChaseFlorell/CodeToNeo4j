using System.Text.Json.Serialization;

namespace CodeToNeo4j.TypeScript.Models;

public class TsAnalysisResult
{
	[JsonPropertyName("projectName")]
	public string ProjectName { get; set; } = string.Empty;

	[JsonPropertyName("projectRoot")]
	public string ProjectRoot { get; set; } = string.Empty;

	[JsonPropertyName("files")]
	public Dictionary<string, TsFileResult> Files { get; set; } = new();
}
