using Microsoft.Build.Locator;

namespace CodeToNeo4j.ProgramOptions;

public class MsBuildRegistrationHandler : OptionsHandler
{
    public override async Task Handle(Options options, HandlerContext context)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
            if (instances.Any())
            {
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
            }
        }

        await base.Handle(options, context);
    }
}
