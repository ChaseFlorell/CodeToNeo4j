using System.Diagnostics.CodeAnalysis;
using CodeToNeo4j.Solution;

namespace CodeToNeo4j.ProgramOptions.Handlers;

[ExcludeFromCodeCoverage]
public class SolutionProcessingHandler(ISolutionProcessor solutionProcessor) : OptionsHandler
{
    protected override async Task<bool> HandleOptions(Options options)
    {
        await solutionProcessor.ProcessSolution(
            options.InputPath,
            options.RepoKey,
            options.DiffBase,
            options.DatabaseName,
            options.BatchSize,
            options.SkipDependencies,
            options.MinAccessibility,
            options.IncludeExtensions);

        return true;
    }
}
