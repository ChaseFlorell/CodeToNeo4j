using System.Text.Json.Serialization;

namespace CodeToNeo4j.Dart.Models;

public class DartAnalysisResult
{
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("projectRoot")]
    public string ProjectRoot { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public Dictionary<string, DartFileResult> Files { get; set; } = new();
}
