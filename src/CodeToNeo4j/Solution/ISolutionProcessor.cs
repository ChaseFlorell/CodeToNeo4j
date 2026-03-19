using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Solution;

public interface ISolutionProcessor
{
	Task ProcessSolution(string inputPath,
		string? repoKey,
		string? diffBase,
		string databaseName,
		int batchSize,
		bool skipDependencies,
		Accessibility minAccessibility,
		IEnumerable<string> includeExtensions);
}
