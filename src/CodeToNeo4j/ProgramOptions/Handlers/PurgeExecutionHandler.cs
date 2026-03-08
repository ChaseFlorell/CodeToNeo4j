using CodeToNeo4j.Graph;

namespace CodeToNeo4j.ProgramOptions.Handlers;

public class PurgeExecutionHandler(IGraphService graphService) : OptionsHandler
{
    protected override async Task<bool> HandleOptions(Options options)
    {
        if (options.PurgeData)
        {
            var allSupportedExtensions = new[] { ".cs", ".razor", ".xaml", ".js", ".html", ".xml", ".json", ".css", ".csproj" };
            var includeExtensions = options.IncludeExtensions.SequenceEqual(allSupportedExtensions) ? null : options.IncludeExtensions;
            await graphService.PurgeData(options.RepoKey, includeExtensions, options.DatabaseName);
            return false;
        }

        return true;
    }
}