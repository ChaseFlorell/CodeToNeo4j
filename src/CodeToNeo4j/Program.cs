using System.CommandLine;
using CodeToNeo4j.ProgramOptions;
using CodeToNeo4j.ProgramOptions.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace CodeToNeo4j;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.WriteLine("Processor Count: {0}", Environment.ProcessorCount);
            return await Run(args);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync(ex.ToString());
            return ex.HResult != 0 ? ex.HResult : 1;
        }
    }

    public static (RootCommand root, OptionsBinder binder) CreateRootCommand()
    {
        string[] allSupportedExtensions = [".cs", ".razor", ".xaml", ".js", ".html", ".xml", ".json", ".css", ".csproj"];
        var uriOption = new Option<string>("--uri")
            .IsRequired()
            .WithDefaultValueFunc(() => "bolt://localhost:7687")
            .WithDescription("The Neo4j connection string.")
            .WithAlias("-u")
            .WithAlias("--url");
        var slnOption = new Option<FileInfo>("--sln")
            .WithAlias("-s")
            .WithDescription("Path to the .sln file to index. Example: ./MySolution.sln");
        var passOption = new Option<string>("--password")
            .IsRequired()
            .WithDescription("Password for the Neo4j database. Example: your-pass")
            .WithAlias("-p");
        var noKeyOption = new Option<bool>("--no-key")
            .WithDescription("Do not use a repository key. Use this if the Neo4j instance is dedicated to this repository.");
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
            quietOption,
            skipDependenciesOption,
            purgeDataOption,
            includeExtensionsOption);

        var root = new RootCommand("Index .NET solution into Neo4j via Roslyn");
        binder.AddToCommand(root);
        root.SetHandler(async options =>
            {
                await using var services = new ServiceCollection()
                    .AddApplicationServices(options.Uri, options.User, options.Pass, options.LogLevel)
                    .BuildServiceProvider();

                var logger = services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("{Options}", options);
                var handlers = services.GetRequiredService<IEnumerable<IOptionsHandler>>();
                await handlers.BuildChain().Handle(options);
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