using CodeToNeo4j.Solution;

namespace CodeToNeo4j.ProgramOptions;

public class SolutionProcessingHandler(ISolutionProcessor solutionProcessor) : OptionsHandler
{
    public override async Task Handle(Options options)
    {
        await solutionProcessor.ProcessSolution(
            options.Sln,
            options.RepoKey,
            options.DiffBase,
            options.DatabaseName,
            options.BatchSize,
            options.SkipDependencies,
            options.MinAccessibility,
            options.IncludeExtensions);

        await base.Handle(options);
    }
}