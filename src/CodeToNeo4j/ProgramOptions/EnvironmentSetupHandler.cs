using CodeToNeo4j.Graph;

namespace CodeToNeo4j.ProgramOptions;

public class EnvironmentSetupHandler : OptionsHandler
{
    public EnvironmentSetupHandler(IGraphService graphService)
    {
        _graphService = graphService;
    }

    public override async Task Handle(Options options)
    {
        await _graphService.Initialize(options.RepoKey, options.DatabaseName);

        await base.Handle(options);
    }

    private readonly IGraphService _graphService;
}
