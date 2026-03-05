using CodeToNeo4j.Solution;
using Microsoft.Extensions.DependencyInjection;

namespace CodeToNeo4j.ProgramOptions;

public class SolutionProcessingHandler : OptionsHandler
{
    public override async Task Handle(Options options, HandlerContext context)
    {
        var serviceProvider = context.ServiceProvider ?? throw new InvalidOperationException("ServiceProvider is not initialized.");
        var processor = serviceProvider.GetRequiredService<ISolutionProcessor>();

        await processor.ProcessSolution(
            options.Sln,
            options.RepoKey,
            options.DiffBase,
            options.DatabaseName,
            options.BatchSize,
            options.SkipDependencies,
            options.MinAccessibility,
            options.IncludeExtensions);

        await base.Handle(options, context);
    }
}
