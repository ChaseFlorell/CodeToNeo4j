using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j;

public class Options(
    FileInfo sln,
    string uri,
    string user,
    string pass,
    string repoKey,
    string? diffBase,
    int batchSize,
    string databaseName,
    LogLevel logLevel,
    bool force,
    bool skipDependencies,
    Accessibility minAccessibility)
{
    public FileInfo Sln { get; } = sln;
    public string Uri { get; } = uri;
    public string User { get; } = user;
    public string Pass { get; } = pass;
    public string RepoKey { get; } = repoKey;
    public string? DiffBase { get; } = diffBase;
    public int BatchSize { get; } = batchSize;
    public string DatabaseName { get; } = databaseName;
    public LogLevel LogLevel { get; } = logLevel;
    public bool Force { get; } = force;
    public bool SkipDependencies { get; } = skipDependencies;
    public Accessibility MinAccessibility { get; } = minAccessibility;
}