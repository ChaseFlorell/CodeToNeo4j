using System.CommandLine;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Solution;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        string[] allSupportedExtensions = [".cs", ".razor", ".xaml", ".js", ".html", ".xml", ".json", ".css", ".csproj"];

        var slnOption = new Option<FileInfo>("--sln", "Path to the .sln file to index. Example: ./MySolution.sln") { IsRequired = true };
        var passOption = new Option<string>("--pass", "Password for the Neo4j database. Example: your-pass") { IsRequired = true };
        var repoKeyOption = new Option<string>("--repoKey", "A unique identifier for the repository in Neo4j. Example: my-repo") { IsRequired = true };
        var uriOption = new Option<string>("--uri", () => "bolt://localhost:7687", "The Neo4j connection string. Example: bolt://localhost:7687");
        var userOption = new Option<string>("--user", () => "neo4j", "Neo4j username. Example: neo4j");
        var databaseOption = new Option<string>("--database", () => "neo4j", "Neo4j database name. Example: my-db");
        var diffBaseOption = new Option<string?>("--diffBase", "Optional git base ref for incremental indexing. Example: origin/main");
        var batchSizeOption = new Option<int>("--batchSize", () => 500, "Number of symbols to batch before flushing to Neo4j. Example: 500");
        var logLevelOption = new Option<LogLevel>("--logLevel", () => LogLevel.Information, "The minimum log level to display. Example: Information");
        var forceOption = new Option<bool>("--force", () => false, "Force reprocessing of the entire solution, even if incremental indexing is enabled. Example: --force");
        var skipDependenciesOption = new Option<bool>("--skip-dependencies", () => false, "Skip NuGet dependency ingestion. Example: --skip-dependencies");
        var minAccessibilityOption = new Option<Accessibility>("--min-accessibility", () => Accessibility.Private, "The minimum accessibility level to index. Default: Private (indices all)");
        var includeExtensionsOption = new Option<string[]>("--include", () => allSupportedExtensions, $"File extensions to include. Supported: {string.Join(", ", allSupportedExtensions)}. Example: --include .cs --include .razor");
        includeExtensionsOption.ArgumentHelpName = string.Join('|', allSupportedExtensions);

        var root = new RootCommand("Index .NET solution into Neo4j via Roslyn")
        {
            slnOption, uriOption, userOption, passOption, repoKeyOption, diffBaseOption, batchSizeOption, databaseOption, logLevelOption, forceOption, skipDependenciesOption, minAccessibilityOption, includeExtensionsOption
        };

        var binder = new OptionsBinder(
            slnOption,
            uriOption,
            userOption,
            passOption,
            repoKeyOption,
            diffBaseOption,
            batchSizeOption,
            databaseOption,
            logLevelOption,
            forceOption,
            skipDependenciesOption,
            minAccessibilityOption,
            includeExtensionsOption
        );

        root.SetHandler(async options =>
            {
                if (!MSBuildLocator.IsRegistered)
                {
                    var instances = MSBuildLocator.QueryVisualStudioInstances().ToList();
                    if (instances.Any())
                    {
                        MSBuildLocator.RegisterInstance(instances.OrderByDescending(x => x.Version).First());
                    }
                }

                var services = new ServiceCollection()
                    .AddApplicationServices(options.Uri, options.User, options.Pass, options.LogLevel);

                await using var serviceProvider = services.BuildServiceProvider();
                var graphService = serviceProvider.GetRequiredService<IGraphService>();
                var processor = serviceProvider.GetRequiredService<ISolutionProcessor>();

                await graphService.Initialize(options.RepoKey, options.DatabaseName);
                await processor.ProcessSolution(options.Sln, options.RepoKey, options.DiffBase, options.DatabaseName, options.BatchSize, options.Force, options.SkipDependencies, options.MinAccessibility, options.IncludeExtensions);
            },
            binder);

        return await root.InvokeAsync(args);
    }
}