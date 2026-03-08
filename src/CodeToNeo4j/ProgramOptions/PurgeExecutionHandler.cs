using CodeToNeo4j.Graph;

namespace CodeToNeo4j.ProgramOptions;

public class PurgeExecutionHandler(IGraphService graphService) : OptionsHandler
{
    public override async Task Handle(Options options)
    {
        if (options.PurgeData)
        {
            var allSupportedExtensions = new[] { ".cs", ".razor", ".xaml", ".js", ".html", ".xml", ".json", ".css", ".csproj" };
            var includeExtensions = options.IncludeExtensions.SequenceEqual(allSupportedExtensions) ? null : options.IncludeExtensions;
            await graphService.PurgeData(options.RepoKey, includeExtensions, options.DatabaseName);
            return; // Terminate chain after purge
        }

        await base.Handle(options);
    }
}
