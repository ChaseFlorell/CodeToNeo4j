using CodeToNeo4j.Solution;

namespace CodeToNeo4j.ProgramOptions;

public class SolutionProcessingHandler(ISolutionProcessor solutionProcessor) : OptionsHandler
{
    protected override async Task<bool> HandleOptions(Options options) =>
        await solutionProcessor.ProcessSolution(
                options.Sln,
                options.RepoKey,
                options.DiffBase,
                options.DatabaseName,
                options.BatchSize,
                options.SkipDependencies,
                options.MinAccessibility,
                options.IncludeExtensions)
            .ContinueWith(_ => true);
}