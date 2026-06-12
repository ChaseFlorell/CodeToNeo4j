using CodeToNeo4j.TypeScript.Models;

namespace CodeToNeo4j.TypeScript.Bridge;

public interface ITypeScriptBridgeService
{
	/// <summary>
	/// Analyzes all JS/TS files under <paramref name="projectRoot"/> and returns the result.
	/// Returns <see langword="null"/> when Node.js is not available or analysis fails.
	/// </summary>
	Task<TsAnalysisResult?> AnalyzeProject(string projectRoot);
}
