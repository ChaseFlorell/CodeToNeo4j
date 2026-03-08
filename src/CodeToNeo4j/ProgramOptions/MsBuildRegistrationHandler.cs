using Microsoft.Build.Locator;

namespace CodeToNeo4j.ProgramOptions;

public class MsBuildRegistrationHandler : OptionsHandler
{
    public override async Task Handle(Options options)
    {
        if (!MSBuildLocator.IsRegistered)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            if (instances.Length != 0)
            {
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
            }
        }

        await base.Handle(options);
    }
}
