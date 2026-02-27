using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using CodeToNeo4j.Console.Cypher;
using CodeToNeo4j.Console.FileSystem;
using CodeToNeo4j.Console.Git;
using CodeToNeo4j.Console.Neo4j;
using Neo4j.Driver;

namespace CodeToNeo4j.Console;

public static class ContainerModule
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, string neo4jUri, string user, string pass, LogLevel minLogLevel)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(minLogLevel);
        });

        services.AddSingleton<IFileSystem, System.IO.Abstractions.FileSystem>();
        services.AddSingleton<CypherService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IGitService, GitService>();
        services.AddSingleton<ISymbolMapper, SymbolMapper>();
        services.AddSingleton<ISolutionProcessor, SolutionProcessor>();
        services.AddSingleton<IDriver>(_ => GraphDatabase.Driver(new Uri(neo4jUri), AuthTokens.Basic(user, pass)));
        services.AddSingleton<INeo4jService, Neo4jService>();

        return services;
    }
}
