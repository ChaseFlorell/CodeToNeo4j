using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j;

public class OptionsBinder(
    Option<FileInfo> slnOption,
    Option<string> uriOption,
    Option<string> userOption,
    Option<string> passOption,
    Option<bool> noKeyOption,
    Option<string?> diffBaseOption,
    Option<int> batchSizeOption,
    Option<string> databaseOption,
    Option<Accessibility> minAccessibilityOption,
    Option<LogLevel> logLevelOption,
    Option<bool> debugOption,
    Option<bool> verboseOption,
    Option<bool> quietOption,
    Option<bool> skipDependenciesOption,
    Option<bool> purgeDataOption,
    Option<string[]> includeExtensionsOption,
    Option<bool> enableCompletionsOption) : BinderBase<Options>
{
    public void Validate(CommandResult result)
    {
        var isEnableCompletions = result.GetValueForOption(enableCompletionsOption);
        if (isEnableCompletions)
        {
            var otherOptionsPresent = result.Children
                .OfType<OptionResult>()
                .Any(or => or.Option != enableCompletionsOption && !or.IsImplicit);

            if (otherOptionsPresent)
            {
                result.ErrorMessage = "No other switches are allowed when using --enable-completions";
            }
            return;
        }

        if (result.FindResultFor(slnOption) is null || result.FindResultFor(slnOption)!.IsImplicit)
        {
            result.ErrorMessage = "Option '--sln' is required.";
        }
        if (result.FindResultFor(passOption) is null || result.FindResultFor(passOption)!.IsImplicit)
        {
            result.ErrorMessage = "Option '--password' is required.";
        }

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
    }

    protected override Options GetBoundValue(BindingContext bindingContext)
    {
        var logLevel = bindingContext.ParseResult.GetValueForOption(logLevelOption);
        if (bindingContext.ParseResult.GetValueForOption(debugOption))
        {
            logLevel = LogLevel.Debug;
        }
        else if (bindingContext.ParseResult.GetValueForOption(verboseOption))
        {
            logLevel = LogLevel.Trace;
        }
        else if (bindingContext.ParseResult.GetValueForOption(quietOption))
        {
            logLevel = LogLevel.None;
        }

        return new(
            bindingContext.ParseResult.GetValueForOption(slnOption)!,
            bindingContext.ParseResult.GetValueForOption(uriOption)!,
            bindingContext.ParseResult.GetValueForOption(userOption)!,
            bindingContext.ParseResult.GetValueForOption(passOption)!,
            bindingContext.ParseResult.GetValueForOption(noKeyOption),
            bindingContext.ParseResult.GetValueForOption(diffBaseOption),
            bindingContext.ParseResult.GetValueForOption(batchSizeOption),
            bindingContext.ParseResult.GetValueForOption(databaseOption)!,
            logLevel,
            bindingContext.ParseResult.GetValueForOption(skipDependenciesOption),
            bindingContext.ParseResult.GetValueForOption(minAccessibilityOption),
            bindingContext.ParseResult.GetValueForOption(includeExtensionsOption)!,
            bindingContext.ParseResult.GetValueForOption(purgeDataOption),
            bindingContext.ParseResult.GetValueForOption(enableCompletionsOption)
        );
    }
}