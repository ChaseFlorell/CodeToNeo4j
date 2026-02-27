using System.CommandLine;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;

namespace CodeToNeo4j.Console;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var slnOption = new Option<FileInfo>("--sln") { IsRequired = true };
        var neo4JOption = new Option<string>("--neo4j", () => "bolt://localhost:7687");
        var userOption = new Option<string>("--user", () => "neo4j");
        var databaseOption = new Option<string>("--database", () => "neo4j");
        var passOption = new Option<string>("--pass") { IsRequired = true };
        var repoKeyOption = new Option<string>("--repoKey") { IsRequired = true };
        var diffBaseOption = new Option<string?>("--diffBase", description: "Optional git base ref for incremental indexing, e.g. origin/main");
        var batchSizeOption = new Option<int>("--batchSize", () => 500);

        var root = new RootCommand("Index C# solution into Neo4j via Roslyn")
        {
            slnOption, neo4JOption, userOption, passOption, repoKeyOption, diffBaseOption, batchSizeOption, databaseOption
        };

        root.SetHandler(async (sln, neo4J, user, pass, repoKey, diffBase, batchSize, databaseName) =>
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
                    .AddApplicationServices(neo4J, user, pass);

                await using var serviceProvider = services.BuildServiceProvider();
                var processor = serviceProvider.GetRequiredService<ISolutionProcessor>();

                await processor.ProcessSolutionAsync(sln, repoKey, diffBase, databaseName, batchSize);
            },
            slnOption,
            neo4JOption,
            userOption,
            passOption,
            repoKeyOption,
            diffBaseOption,
            batchSizeOption,
            databaseOption);

        return await root.InvokeAsync(args);
    }
}