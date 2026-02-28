using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using CodeToNeo4j.Cypher;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Git;
using CodeToNeo4j.Neo4j;
using CodeToNeo4j.Progress;
using Neo4j.Driver;

namespace CodeToNeo4j;

public static class ContainerModule
{
    /// <summary>
    /// Configures and registers application services into the provided <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to which the application services will be added.</param>
    /// <param name="neo4jUri">The URI of the Neo4j database to connect to.</param>
    /// <param name="user">The username for authenticating with the Neo4j database.</param>
    /// <param name="pass">The password for authenticating with the Neo4j database.</param>
    /// <param name="minLogLevel">The minimum log level for the application's logging configuration.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> containing the registered application services.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services,
        string neo4jUri,
        string user,
        string pass,
        LogLevel minLogLevel)
    {
        services.AddLogging(builder =>
        {
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(minLogLevel);
        });

        services.AddSingleton<IFileSystem, System.IO.Abstractions.FileSystem>();
        services.AddSingleton<ICypherService, CypherService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<ISymbolMapper, SymbolMapper>();

        services.AddSingleton<IDocumentHandler, CSharpHandler>();
        services.AddSingleton<IDocumentHandler, RazorHandler>();
        services.AddSingleton<IDocumentHandler, XamlHandler>();

        services.AddSingleton<ISolutionProcessor, SolutionProcessor>();

        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")))
        {
            services.AddSingleton<IProgressService, GitHubActionsProgressService>();
        }
        else if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TF_BUILD")))
        {
            services.AddSingleton<IProgressService, AzureDevOpsProgressService>();
        }
        else
        {
            services.AddSingleton<IProgressService, ConsoleProgressService>();
        }

        services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(new Uri(neo4jUri), AuthTokens.Basic(user, pass)));
        services.AddSingleton<INeo4jService, Neo4jService>();

        return services;
    }
}