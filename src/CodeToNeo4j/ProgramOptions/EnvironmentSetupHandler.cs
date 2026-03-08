using CodeToNeo4j.Graph;
using Microsoft.Extensions.DependencyInjection;

namespace CodeToNeo4j.ProgramOptions;

public class EnvironmentSetupHandler : OptionsHandler
{
    public override async Task Handle(Options options)
    {
        var services = new ServiceCollection()
            .AddApplicationServices(options.Uri, options.User, options.Pass, options.LogLevel);

        await using var serviceProvider = services.BuildServiceProvider();
        var graphService = serviceProvider.GetRequiredService<IGraphService>();

        await graphService.Initialize(options.RepoKey, options.DatabaseName);

        await base.Handle(options);
    }
}
