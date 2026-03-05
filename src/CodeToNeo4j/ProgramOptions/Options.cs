using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.ProgramOptions;

public sealed record Options(
    FileInfo Sln,
    string Uri,
    string User,
    string Pass,
    bool NoKey,
    string? DiffBase,
    int BatchSize,
    string DatabaseName,
    LogLevel LogLevel,
    bool SkipDependencies,
    Accessibility MinAccessibility,
    IEnumerable<string> IncludeExtensions,
    bool PurgeData)
{
    public bool HasRepoKey { get; } = !NoKey;
    public string? RepoKey => HasRepoKey ? Path.GetFileNameWithoutExtension(Sln.Name).ToLowerInvariant() : null;
}