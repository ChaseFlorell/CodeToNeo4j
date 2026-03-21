using System.Collections.Concurrent;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Solution.Discovery;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Solution.Ingestion;

public class DependencyIngestor(
	IGraphService graphService,
	ILogger<DependencyIngestor> logger) : IDependencyIngestor
{
	public async Task IngestDependencies(Microsoft.CodeAnalysis.Solution solution, string? repoKey, string databaseName)
	{
		logger.LogInformation("Ingesting NuGet dependencies...");

		ConcurrentBag<Dependency> dependencies = [];
		ParallelOptions parallelOptions = new()
		{
			MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount),
			CancellationToken = CancellationToken.None
		};

		// Filter out outer multi-target wrapper projects (they have 0 documents and produce empty compilations).
		// For multi-target projects, only compile one representative per base project name since
		// TFM variants share ~95% of the same NuGet dependencies and the result is deduplicated anyway.
		ConcurrentDictionary<string, byte> processedBaseNames = new(StringComparer.OrdinalIgnoreCase);
		var projects = solution.Projects.Where(p => p.Documents.Any() || p.AdditionalDocuments.Any());

		await Parallel.ForEachAsync(projects, parallelOptions, (project, token) =>
				ProcessProject(dependencies, processedBaseNames, project, token))
			.ConfigureAwait(false);

		var uniqueDeps = dependencies.DistinctBy(d => d.Key)
			.OrderBy(d => d.Key)
			.ToArray();

		await graphService.UpsertDependencies(repoKey, uniqueDeps, databaseName)
			.ConfigureAwait(false);

		logger.LogInformation("Ingested {Count} unique dependencies.", uniqueDeps.Length);
	}

	private async ValueTask ProcessProject(ConcurrentBag<Dependency> dependencies, ConcurrentDictionary<string, byte> processedBaseNames,
		Project project, CancellationToken token)
	{
		if (!project.SupportsCompilation)
		{
			return;
		}

		// For multi-target projects (e.g. "Serilog(net9.0)", "Serilog(net8.0)"), only compile one
		// representative per base project name. The TFM variants share nearly identical dependencies
		// and the results are deduplicated via DistinctBy afterward.
		var baseName = ExtractBaseProjectName(project.Name);
		if (!processedBaseNames.TryAdd(baseName, 0))
		{
			logger.LogDebug("Skipping dependency extraction for {ProjectName} — already extracted from another TFM of {BaseName}", project.Name,
				baseName);
			return;
		}

		Compilation? compilation;
		try
		{
			compilation = await project.GetCompilationAsync(token)
				.ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			logger.LogDebug(ex,
				"Failed to get compilation for project {ProjectName}, skipping dependency extraction. "
				+ "This is common with multi-target projects where the outer wrapper project lacks a Compile target. "
				+ "Dependencies will still be extracted from the specific target framework projects (e.g. net9.0, net8.0)",
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
			var simpleName = name.Split(',')[0].Split(':')[0].Trim();
			var key = $"pkg:{simpleName}";
			dependencies.Add(new(key, simpleName, reference.Version.ToString()));
		}
	}

	internal static string ExtractBaseProjectName(string projectName)
	{
		var tfm = SolutionFileDiscoveryService.ExtractTargetFramework(projectName);
		return tfm is null ? projectName : projectName[..^(tfm.Length + 2)]; // strip "(tfm)"
	}
}
