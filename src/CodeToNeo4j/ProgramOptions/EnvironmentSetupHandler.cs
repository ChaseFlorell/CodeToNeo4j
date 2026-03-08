using CodeToNeo4j.Graph;

namespace CodeToNeo4j.ProgramOptions;

public class EnvironmentSetupHandler(IGraphService graphService) : OptionsHandler
{
    protected override async Task<bool> HandleOptions(Options options) =>
        await graphService.Initialize(options.RepoKey, options.DatabaseName)
            .ContinueWith(_ => true);
}