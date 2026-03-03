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
        try
        {
            return await Run(args);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.ToString());
            return ex.HResult != 0 ? ex.HResult : 1;
        }
    }

    private static async Task<int> Run(string[] args)
    {
        string[] allSupportedExtensions = [".cs", ".razor", ".xaml", ".js", ".html", ".xml", ".json", ".css", ".csproj"];

        var slnOption = new Option<FileInfo?>("--sln", "Path to the .sln file to index. Example: ./MySolution.sln");
        var passOption = new Option<string>("--password", "Password for the Neo4j database. Example: your-pass") { IsRequired = true };
        var repoKeyOption = new Option<string>("--repository-key", "A unique identifier for the repository in Neo4j. Example: my-repo") { IsRequired = true };
        var uriOption = new Option<string>("--uri", () => "bolt://localhost:7687", "The Neo4j connection string. Example: bolt://localhost:7687");
        var userOption = new Option<string>("--user", () => "neo4j", "Neo4j username. Example: neo4j");
        var databaseOption = new Option<string>("--database", () => "neo4j", "Neo4j database name. Example: my-db");
        var diffBaseOption = new Option<string?>("--diff-base", "Optional git base ref for incremental indexing. Example: origin/main");
        var batchSizeOption = new Option<int>("--batch-size", () => 500, "Number of symbols to batch before flushing to Neo4j. Example: 500");
        var logLevelOption = new Option<LogLevel>("--log-level", () => LogLevel.Information, "The minimum log level to display. Example: Information");
        var skipDependenciesOption = new Option<bool>("--skip-dependencies", () => false, "Skip NuGet dependency ingestion. Example: --skip-dependencies");
        var minAccessibilityOption = new Option<Accessibility>("--min-accessibility", () => Accessibility.Private, "The minimum accessibility level to index. Default: Private (indices all)");
        var purgeDataByRepoKeyOption = new Option<bool>("--purge-data-by-repository-key", () => false, "Purge all data from Neo4j associated with the specified repoKey. Example: --purge-data-by-repoKey");
        var includeExtensionsOption = new Option<string[]>("--include", () => allSupportedExtensions, $"File extensions to include. Supported: {string.Join(", ", allSupportedExtensions)}. Example: --include .cs --include .razor");
        var debugOption = new Option<bool>("--debug", "Turn on debug logging. Example: --debug");
        var verboseOption = new Option<bool>("--verbose", "Turn on trace logging. Example: --verbose");
        var quietOption = new Option<bool>("--quiet", "Mute all logging output. Example: --quiet");

        passOption.AddAlias("-p");
        repoKeyOption.AddAlias("-r");
        uriOption.AddAlias("-u");
        uriOption.AddAlias("--url");
        databaseOption.AddAlias("-db");
        logLevelOption.AddAlias("-l");
        includeExtensionsOption.AddAlias("-i");
        debugOption.AddAlias("-d");
        verboseOption.AddAlias("-v");
        quietOption.AddAlias("-q");

        includeExtensionsOption.ArgumentHelpName = string.Join('|', allSupportedExtensions);


        var root = new RootCommand("Index .NET solution into Neo4j via Roslyn")
        {
            slnOption, uriOption, userOption, passOption, repoKeyOption, diffBaseOption, batchSizeOption, databaseOption, logLevelOption, skipDependenciesOption, minAccessibilityOption, includeExtensionsOption, purgeDataByRepoKeyOption, debugOption, verboseOption, quietOption
        };

        root.AddValidator(result =>
        {
            var usedLogLevel = result.FindResultFor(logLevelOption) is not null && !result.FindResultFor(logLevelOption)!.IsImplicit;
            var usedDebug = result.FindResultFor(debugOption) is not null;
            var usedVerbose = result.FindResultFor(verboseOption) is not null;
            var usedQuiet = result.FindResultFor(quietOption) is not null;

            int logOptionsCount = (usedLogLevel ? 1 : 0) + (usedDebug ? 1 : 0) + (usedVerbose ? 1 : 0) + (usedQuiet ? 1 : 0);
            if (logOptionsCount > 1)
            {
                result.ErrorMessage = "Only one of --log-level, --debug, --verbose, or --quiet can be used.";
            }

            var isPurge = result.GetValueForOption(purgeDataByRepoKeyOption);
            if (isPurge)
            {
                if (result.GetValueForOption(skipDependenciesOption))
                {
                    result.ErrorMessage = "--skip-dependencies is not allowed when using --purge-data-by-repoKey";
                }

                if (result.GetValueForOption(minAccessibilityOption) != Accessibility.Private)
                {
                    result.ErrorMessage = "--min-accessibility is not allowed when using --purge-data-by-repoKey";
                }
            }
            else if (result.GetValueForOption(slnOption) == null)
            {
                result.ErrorMessage = "--sln is required when not using --purge-data-by-repoKey";
            }
        });

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
            skipDependenciesOption,
            minAccessibilityOption,
            includeExtensionsOption,
            purgeDataByRepoKeyOption,
            debugOption,
            verboseOption,
            quietOption
        );

        root.SetHandler(async (Options options) =>
            {
                if (options.PurgeDataByRepoKey)
                {
                    Console.Write($"Are you sure you want to purge all data for repoKey '{options.RepoKey}'? (y/n): ");
                    var response = Console.ReadLine();
                    if (response?.ToLower() != "y")
                    {
                        Console.WriteLine("Purge aborted.");
                        return;
                    }
                }

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

                if (options.PurgeDataByRepoKey)
                {
                    var includeExtensions = options.IncludeExtensions.SequenceEqual(allSupportedExtensions) ? null : options.IncludeExtensions;
                    await graphService.PurgeData(options.RepoKey, includeExtensions, options.DatabaseName);
                    return;
                }

                await processor.ProcessSolution(options.Sln!, options.RepoKey, options.DiffBase, options.DatabaseName, options.BatchSize, options.SkipDependencies, options.MinAccessibility, options.IncludeExtensions);
            },
            binder);

        return await root.InvokeAsync(args);
    }
}