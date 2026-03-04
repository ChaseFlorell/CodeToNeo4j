using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Solution;

public interface ISolutionProcessor
{
    Task ProcessSolution(FileInfo sln, string? repoKey, string? diffBase, string databaseName, int batchSize, bool skipDependencies, Accessibility minAccessibility, IEnumerable<string> includeExtensions);
}