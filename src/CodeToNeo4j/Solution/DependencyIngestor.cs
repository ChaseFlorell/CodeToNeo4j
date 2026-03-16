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

        // Filter out outer multi-target wrapper projects (they have 0 documents and produce empty compilations)
        var projects = solution.Projects.Where(p => p.Documents.Any() || p.AdditionalDocuments.Any());

        await Parallel.ForEachAsync(projects, parallelOptions, (project, token) =>
                ProcessProject(dependencies, project, token))
            .ConfigureAwait(false);

        var uniqueDeps = dependencies.DistinctBy(d => d.Key)
            .OrderBy(d => d.Key)
            .ToArray();

        await graphService.UpsertDependencies(repoKey, uniqueDeps, databaseName)
            .ConfigureAwait(false);

        logger.LogInformation("Ingested {Count} unique dependencies.", uniqueDeps.Length);
    }

    private async ValueTask ProcessProject(ConcurrentBag<Dependency> dependencies, Project project, CancellationToken token)
    {
        if (!project.SupportsCompilation)
        {
            return;
        }

        Compilation? compilation;
        try
        {
            // Project.GetCompilationAsync is expensive because it can trigger a full build.
            // However, for extracting NuGet dependencies, Roslyn must at least resolve metadata references.
            // We use GetCompilationAsync but rely on Roslyn's internal caching.
            // Once a project is compiled, subsequent calls for the same project in the same solution
            // will be much faster.
            compilation = await project.GetCompilationAsync(token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex,
                "Failed to get compilation for project {ProjectName}, skipping dependency extraction. "
                + "This is common with multi-target projects where the outer wrapper project lacks a Compile target",
                project.Name);
            return;
        }

        if (compilation is null)
        {
            return;
        }

        foreach (var reference in compilation.ReferencedAssemblyNames)
        {
            var name = reference.Name;
            // Ensure we use the simple name for the key to avoid including version or culture info.
            var simpleName = name.Split(',')[0].Split(':')[0].Trim();
            var key = $"pkg:{simpleName}";
            dependencies.Add(new Dependency(key, simpleName, reference.Version.ToString()));
        }
    }
}
