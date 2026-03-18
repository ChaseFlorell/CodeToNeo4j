using CodeToNeo4j.Dart.Models;

namespace CodeToNeo4j.Dart.Bridge;

public interface IDartBridgeService
{
    Task<DartAnalysisResult?> AnalyzeProject(string projectRoot);
}
