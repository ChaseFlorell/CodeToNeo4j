using System.CommandLine;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Console;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var slnOption = new Option<FileInfo>("--sln") { IsRequired = true };
        var passOption = new Option<string>("--pass") { IsRequired = true };
        var repoKeyOption = new Option<string>("--repoKey") { IsRequired = true };
        var neo4JOption = new Option<string>("--neo4j", () => "bolt://localhost:7687");
        var userOption = new Option<string>("--user", () => "neo4j");
        var databaseOption = new Option<string>("--database", () => "neo4j");
        var diffBaseOption = new Option<string?>("--diffBase", description: "Optional git base ref for incremental indexing, e.g. origin/main");
        var batchSizeOption = new Option<int>("--batchSize", () => 500);
        var logLevelOption = new Option<LogLevel>("--logLevel", () => LogLevel.Information, "The minimum log level to display.");

        var root = new RootCommand("Index C# solution into Neo4j via Roslyn")
        {
            slnOption, neo4JOption, userOption, passOption, repoKeyOption, diffBaseOption, batchSizeOption, databaseOption, logLevelOption
        };

        var binder = new OptionsBinder(
            slnOption, neo4JOption, userOption, passOption, repoKeyOption, diffBaseOption, batchSizeOption, databaseOption, logLevelOption
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
                    .AddApplicationServices(options.Neo4j, options.User, options.Pass, options.LogLevel);

                await using var serviceProvider = services.BuildServiceProvider();
                var processor = serviceProvider.GetRequiredService<ISolutionProcessor>();

                await processor.ProcessSolutionAsync(options.Sln, options.RepoKey, options.DiffBase, options.DatabaseName, options.BatchSize);
            },
            binder);

        return await root.InvokeAsync(args);
    }
}