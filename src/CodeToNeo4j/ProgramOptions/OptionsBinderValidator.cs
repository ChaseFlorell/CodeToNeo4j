using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.ProgramOptions;

public static class OptionsBinderValidator
{
    public static void Validate(
        in CommandResult result,
        Option<FileInfo> slnOption,
        Option<bool> noKeyOption,
        Option<LogLevel> logLevelOption,
        Option<bool> debugOption,
        Option<bool> verboseOption,
        Option<bool> quietOption,
        Option<bool> purgeDataOption,
        Option<bool> skipDependenciesOption,
        Option<Accessibility> minAccessibilityOption)
    {
        var isPurge = result.GetValueForOption(purgeDataOption);
        var noKey = result.GetValueForOption(noKeyOption);
        var sln = result.GetValueForOption(slnOption);

        if (isPurge)
        {
            if (!noKey && sln is null)
            {
                result.ErrorMessage = "--sln is required when using --purge-data without --no-key";
            }

            var minAccResult = result.FindResultFor(minAccessibilityOption);
            if (minAccResult is not null && !minAccResult.IsImplicit)
            {
                result.ErrorMessage = "--min-accessibility is not allowed when using --purge-data";
            }
        }
        else
        {
            if (sln is null)
            {
                result.ErrorMessage = "--sln is required";
            }
        }

        var usedLogLevel = result.FindResultFor(logLevelOption) is not null && !result.FindResultFor(logLevelOption)!.IsImplicit;
        var usedDebug = result.FindResultFor(debugOption) is not null;
        var usedVerbose = result.FindResultFor(verboseOption) is not null;
        var usedQuiet = result.FindResultFor(quietOption) is not null;

        var logOptionsCount = (usedLogLevel ? 1 : 0) + (usedDebug ? 1 : 0) + (usedVerbose ? 1 : 0) + (usedQuiet ? 1 : 0);
        if (logOptionsCount > 1)
        {
            result.ErrorMessage = "Only one of --log-level, --debug, --verbose, or --quiet can be used.";
        }
    }
}