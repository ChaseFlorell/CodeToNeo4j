using CodeToNeo4j.Graph;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Solution;

public class DependencyIngestor(
    IGraphService graphService,
    ILogger<DependencyIngestor> logger) : IDependencyIngestor
{
    public async ValueTask IngestDependencies(Microsoft.CodeAnalysis.Solution solution, string repoKey, string databaseName)
    {
        logger.LogInformation("Ingesting NuGet dependencies...");
        var dependencies = new List<Dependency>();
        foreach (var project in solution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation is null) continue;

            foreach (var reference in compilation.ReferencedAssemblyNames)
            {
                var key = $"pkg:{reference.Name}:{reference.Version}";
                dependencies.Add(new Dependency(key, reference.Name, reference.Version.ToString()));
            }
        }

        var uniqueDeps = dependencies.DistinctBy(d => d.Key).ToArray();
        await graphService.UpsertDependencies(repoKey, uniqueDeps, databaseName);
        logger.LogInformation("Ingested {Count} unique dependencies.", uniqueDeps.Length);
    }
}
