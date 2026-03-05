using CodeToNeo4j.Graph;
using Microsoft.Extensions.DependencyInjection;

namespace CodeToNeo4j.ProgramOptions;

public class PurgeExecutionHandler(string[] allSupportedExtensions) : OptionsHandler
{
    public override async Task Handle(Options options, HandlerContext context)
    {
        if (options.PurgeData)
        {
            var serviceProvider = context.ServiceProvider ?? throw new InvalidOperationException("ServiceProvider is not initialized.");
            var graphService = serviceProvider.GetRequiredService<IGraphService>();

            var includeExtensions = options.IncludeExtensions.SequenceEqual(allSupportedExtensions) ? null : options.IncludeExtensions;
            await graphService.PurgeData(options.RepoKey, includeExtensions, options.DatabaseName);
            return; // Terminate chain after purge
        }

        await base.Handle(options, context);
    }
}
