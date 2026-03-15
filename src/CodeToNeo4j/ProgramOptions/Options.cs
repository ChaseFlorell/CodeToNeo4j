using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.ProgramOptions;

public sealed record Options(
    FileInfo Sln,
    string Uri,
    string User,
    string? Pass,
    bool NoKey,
    string? DiffBase,
    int BatchSize,
    string DatabaseName,
    LogLevel LogLevel,
    bool SkipDependencies,
    Accessibility MinAccessibility,
    IEnumerable<string> IncludeExtensions,
    bool PurgeData,
    bool ShowVersion,
    bool ShowSupportedFiles,
    bool ShowInfo)
{
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        builder.AppendLine("");
        builder.AppendLine($"\tSln = {Sln}, ");
        builder.AppendLine($"\tUri = {Uri}, ");
        builder.AppendLine($"\tUser = {User}, ");
        builder.AppendLine($"\tPass = {Pass}, ");
        builder.AppendLine($"\tNoKey = {NoKey}, ");
        builder.AppendLine($"\tDiffBase = {DiffBase}, ");
        builder.AppendLine($"\tBatchSize = {BatchSize}, ");
        builder.AppendLine($"\tDatabaseName = {DatabaseName}, ");
        builder.AppendLine($"\tLogLevel = {LogLevel}, ");
        builder.AppendLine($"\tSkipDependencies = {SkipDependencies}, ");
        builder.AppendLine($"\tMinAccessibility = {MinAccessibility}, ");
        builder.AppendLine($"\tIncludeExtensions = [ {string.Join(", ", IncludeExtensions)} ], ");
        builder.AppendLine($"\tPurgeData = {PurgeData}");
        builder.AppendLine($"\tShowVersion = {ShowVersion}");
        builder.AppendLine($"\tShowSupportedFiles = {ShowSupportedFiles}");
        builder.AppendLine($"\tShowInfo = {ShowInfo}");
        return true;
    }

    public string? RepoKey =>
        NoKey
            ? null
            : Path.GetFileNameWithoutExtension(Sln.Name).ToLowerInvariant();
}
