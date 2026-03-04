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

    public static (RootCommand root, OptionsBinder binder) CreateRootCommand()
    {
        string[] allSupportedExtensions = [".cs", ".razor", ".xaml", ".js", ".html", ".xml", ".json", ".css", ".csproj"];

        var slnOption = new Option<FileInfo>("--sln")
            .WithAlias("-s")
            .WithDescription("Path to the .sln file to index. Example: ./MySolution.sln")
            .IsRequired();
        var passOption = new Option<string>("--password")
            .WithDescription("Password for the Neo4j database. Example: your-pass")
            .IsRequired()
            .WithAlias("-p");
        var noKeyOption = new Option<bool>("--no-key")
            .WithDescription("Do not use a repository key. Use this if the Neo4j instance is dedicated to this repository.");
        var uriOption = new Option<string>("--uri")
            .WithDefaultValueFunc(() => "bolt://localhost:7687")
            .WithDescription("The Neo4j connection string.")
            .WithAlias("-u")
            .WithAlias("--url");
        var userOption = new Option<string>("--user")
            .WithDefaultValueFunc(() => "neo4j")
            .WithDescription("Neo4j username.");
        var databaseOption = new Option<string>("--database")
            .WithDefaultValueFunc(() => "neo4j")
            .WithDescription("Neo4j database name.")
            .WithAlias("-db");
        var diffBaseOption = new Option<string?>("--diff-base")
            .WithDescription("Optional git base ref for incremental indexing. Example: origin/main");
        var batchSizeOption = new Option<int>("--batch-size")
            .WithDefaultValueFunc(() => 500)
            .WithDescription("Number of symbols to batch before flushing to Neo4j.");
        var logLevelOption = new Option<LogLevel>("--log-level")
            .WithDefaultValueFunc(() => LogLevel.Information)
            .WithDescription("The minimum log level to display.")
            .WithAlias("-l");
        var skipDependenciesOption = new Option<bool>("--skip-dependencies")
            .WithDefaultValueFunc(() => false)
            .WithDescription("Skip NuGet dependency ingestion. Example: --skip-dependencies");
        var minAccessibilityOption = new Option<Accessibility>("--min-accessibility")
            .WithDefaultValueFunc(() => Accessibility.Private)
            .WithArgumentHelpName(string.Join('|', Accessibility.Private, Accessibility.Internal, Accessibility.Protected, Accessibility.Public))
            .WithDescription("The minimum accessibility level to index.");
        var includeExtensionsOption = new Option<string[]>("--include")
            .WithDefaultValueFunc(() => allSupportedExtensions)
            .WithDescription($"File extensions to include. Example: --include .cs --include .razor")
            .WithAlias("-i");
        var debugOption = new Option<bool>("--debug")
            .WithDescription("Turn on debug logging. Example: --debug")
            .WithAlias("-d");
        var verboseOption = new Option<bool>("--verbose")
            .WithDescription("Turn on trace logging. Example: --verbose")
            .WithAlias("-v");
        var quietOption = new Option<bool>("--quiet")
            .WithDescription("Mute all logging output. Example: --quiet")
            .WithAlias("-q");
        var purgeDataOption = new Option<bool>("--purge-data")
            .WithDefaultValueFunc(() => false)
            .WithDescription("Purge all data from Neo4j associated with the specified repository key. Example: --purge-data");

        var root = new RootCommand("Index .NET solution into Neo4j via Roslyn")
        {
            slnOption, uriOption, userOption, passOption, noKeyOption, diffBaseOption, batchSizeOption, databaseOption, logLevelOption, skipDependenciesOption, minAccessibilityOption, includeExtensionsOption, purgeDataOption, debugOption, verboseOption, quietOption
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

            var isPurge = result.GetValueForOption(purgeDataOption);
            if (isPurge)
            {
                if (result.GetValueForOption(skipDependenciesOption))
                {
                    result.ErrorMessage = "--skip-dependencies is not allowed when using --purge-data";
                }

                if (result.GetValueForOption(minAccessibilityOption) != Accessibility.Private)
                {
                    result.ErrorMessage = "--min-accessibility is not allowed when using --purge-data";
                }
            }
        });

        var binder = new OptionsBinder(
            slnOption,
            uriOption,
            userOption,
            passOption,
            noKeyOption,
            diffBaseOption,
            batchSizeOption,
            databaseOption,
            minAccessibilityOption,
            logLevelOption,
            debugOption,
            verboseOption,
            quietOption, skipDependenciesOption, purgeDataOption, includeExtensionsOption);

        root.SetHandler(async options =>
            {
                if (options.PurgeData)
                {
                    var purgeTarget = options.RepoKey is null ? "ALL CodeToNeo4j data" : $"all data for repository key '{options.RepoKey}'";
                    Console.Write($"Are you sure you want to purge {purgeTarget}? (y/n): ");
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

                if (options.PurgeData)
                {
                    var includeExtensions = options.IncludeExtensions.SequenceEqual(allSupportedExtensions) ? null : options.IncludeExtensions;
                    await graphService.PurgeData(options.RepoKey, includeExtensions, options.DatabaseName);
                    return;
                }

                await processor.ProcessSolution(options.Sln, options.RepoKey, options.DiffBase, options.DatabaseName, options.BatchSize, options.SkipDependencies, options.MinAccessibility, options.IncludeExtensions);
            },
            binder);

        return (root, binder);
    }

    private static async Task<int> Run(string[] args)
    {
        var (root, _) = CreateRootCommand();
        return await root.InvokeAsync(args);
    }
}