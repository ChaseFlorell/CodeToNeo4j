using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using Microsoft.Build.Locator;

namespace CodeToNeo4j.ProgramOptions.Handlers;

[ExcludeFromCodeCoverage]
public class MsBuildRegistrationHandler(IFileSystem fileSystem) : OptionsHandler
{
    protected override Task<bool> HandleOptions(Options options)
    {
        // Skip MSBuild registration for directory/files-only mode
        if (fileSystem.Directory.Exists(options.InputPath))
            return Task.FromResult(true);


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
