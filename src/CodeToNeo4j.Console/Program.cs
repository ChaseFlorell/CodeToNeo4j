using System.CommandLine;
using System.IO.Abstractions;
using Microsoft.Build.Locator;
using Microsoft.Extensions.DependencyInjection;
using Neo4j.Driver;

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
            if (!MSBuildLocator.IsRegistered) MSBuildLocator.RegisterDefaults();

            var services = new ServiceCollection();
            services.AddSingleton<IFileSystem, FileSystem>();
            services.AddSingleton<CypherService>();
            services.AddSingleton<IFileService, FileService>();
            services.AddSingleton<IGitService, GitService>();
            services.AddSingleton<ISymbolMapper, SymbolMapper>();
            services.AddSingleton<ISolutionProcessor, SolutionProcessor>();
            services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(new Uri(neo4J), AuthTokens.Basic(user, pass)));
            services.AddSingleton<INeo4jService, Neo4jService>();

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