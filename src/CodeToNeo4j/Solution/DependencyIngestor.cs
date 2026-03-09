using System.Collections.Concurrent;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Solution;

public class DependencyIngestor(
    IGraphService graphService,
    ILogger<DependencyIngestor> logger) : IDependencyIngestor
{
    public async Task IngestDependencies(Microsoft.CodeAnalysis.Solution solution, string? repoKey, string databaseName)
    {
        logger.LogInformation("Ingesting NuGet dependencies...");

        // Parallelizing project compilation can be memory intensive.
        // We'll process projects in batches to avoid overwhelming the system while still being faster than sequential.
        var dependencies = new ConcurrentBag<Dependency>();
        var parallelOptions = new ParallelOptions
        {
            // Reduced from 20 to a more balanced number based on processor count to avoid high memory pressure
            // while still maintaining high throughput.
            MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
            CancellationToken = CancellationToken.None
        };

        await Parallel.ForEachAsync(solution.Projects, parallelOptions, (project, token) =>
                ProcessProject(dependencies, project, token))
            .ConfigureAwait(false);

        var uniqueDeps = dependencies.DistinctBy(d => d.Key)
            .OrderBy(d => d.Key)
            .ToArray();

        await graphService.UpsertDependencies(repoKey, uniqueDeps, databaseName)
            .ConfigureAwait(false);

        logger.LogInformation("Ingested {Count} unique dependencies.", uniqueDeps.Length);
    }

    private static async ValueTask ProcessProject(ConcurrentBag<Dependency> dependencies, Project project, CancellationToken token)
    {
        if (!project.SupportsCompilation)
        {
            return;
        }

        // Project.GetCompilationAsync is expensive because it can trigger a full build.
        // However, for extracting NuGet dependencies, Roslyn must at least resolve metadata references.
        // We use GetCompilationAsync but rely on Roslyn's internal caching.
        // Once a project is compiled, subsequent calls for the same project in the same solution 
        // will be much faster.
        var compilation = await project.GetCompilationAsync(token)
            .ConfigureAwait(false);

        if (compilation is null)
        {
            return;
        }

        foreach (var reference in compilation.ReferencedAssemblyNames)
        {
            var key = $"pkg:{reference.Name}:{reference.Version}";
            dependencies.Add(new Dependency(key, reference.Name, reference.Version.ToString()));
        }
    }
}