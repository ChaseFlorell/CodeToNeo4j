using CodeToNeo4j.Solution;

namespace CodeToNeo4j.ProgramOptions;

public class SolutionProcessingHandler : OptionsHandler
{
    public SolutionProcessingHandler(ISolutionProcessor solutionProcessor) => _solutionProcessor = solutionProcessor;

    public override async Task Handle(Options options)
    {
        await _solutionProcessor.ProcessSolution(
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

    private readonly ISolutionProcessor _solutionProcessor;
}