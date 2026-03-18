using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Microsoft.Build.Locator;

namespace CodeToNeo4j.ProgramOptions.Handlers;

[ExcludeFromCodeCoverage(Justification = "Invokes MSBuildLocator against the real MSBuild installation; not unit testable")]
public class MsBuildRegistrationHandler : OptionsHandler
{
    protected override Task<bool> HandleOptions(Options options)
    {
        // Skip MSBuild registration for directory/files-only mode
        if (options.InputPath is IDirectoryInfo)
        {
            return Task.FromResult(true);
        }

        if (!MSBuildLocator.IsRegistered)
        {
            var instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
            if (instances.Length != 0)
            {
                MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
            }
        }

        return Task.FromResult(true);
    }
}
