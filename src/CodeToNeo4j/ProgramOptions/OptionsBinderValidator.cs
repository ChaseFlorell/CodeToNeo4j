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
        Option<Accessibility> minAccessibilityOption,
        Option<string> passOption,
        Option<bool> showVersionOption,
        Option<bool> showSupportedFilesOption,
        Option<bool> showInfoOption)
    {
        var isInfo = result.GetValue(showVersionOption)
                     || result.GetValue(showSupportedFilesOption)
                     || result.GetValue(showInfoOption);

        if (isInfo)
            return;

        var isPurge = result.GetValue(purgeDataOption);
        var noKey = result.GetValue(noKeyOption);
        var sln = result.GetValue(slnOption);
        var pass = result.GetResult(passOption);

        if (pass is null or { Implicit: true })
        {
            result.AddError("--password is required");
            return;
        }

        if (isPurge)
        {
            if (!noKey && sln is null)
            {
                result.AddError("--sln is required when using --purge-data without --no-key");
            }

            var minAccResult = result.GetResult(minAccessibilityOption);
            if (minAccResult is not null && !minAccResult.Implicit)
            {
                result.AddError("--min-accessibility is not allowed when using --purge-data");
            }
        }
        else
        {
            if (sln is null)
            {
                result.AddError("--sln is required");
            }
        }

        var usedLogLevel = result.GetResult(logLevelOption) is { Implicit: false };
        var usedDebug = result.GetResult(debugOption) is { Implicit: false };
        var usedVerbose = result.GetResult(verboseOption) is { Implicit: false };
        var usedQuiet = result.GetResult(quietOption) is { Implicit: false };

        var logOptionsCount = (usedLogLevel ? 1 : 0) + (usedDebug ? 1 : 0) + (usedVerbose ? 1 : 0) + (usedQuiet ? 1 : 0);
        if (logOptionsCount > 1)
        {
            result.AddError("Only one of --log-level, --debug, --verbose, or --quiet can be used.");
        }
    }
}
