using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Console;

public class Options(
    FileInfo sln,
    string neo4j,
    string user,
    string pass,
    string repoKey,
    string? diffBase,
    int batchSize,
    string databaseName,
    LogLevel logLevel)
{
    public FileInfo Sln { get; } = sln;
    public string Neo4j { get; } = neo4j;
    public string User { get; } = user;
    public string Pass { get; } = pass;
    public string RepoKey { get; } = repoKey;
    public string? DiffBase { get; } = diffBase;
    public int BatchSize { get; } = batchSize;
    public string DatabaseName { get; } = databaseName;
    public LogLevel LogLevel { get; } = logLevel;
}