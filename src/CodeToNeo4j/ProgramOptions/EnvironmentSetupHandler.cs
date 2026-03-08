using CodeToNeo4j.Graph;

namespace CodeToNeo4j.ProgramOptions;

public class EnvironmentSetupHandler(IGraphService graphService) : OptionsHandler
{
    public override async Task Handle(Options options)
    {
        await graphService.Initialize(options.RepoKey, options.DatabaseName);

        await base.Handle(options);
    }
}
