using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.ProgramOptions;

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
    Option<bool> showVersionOption,
    Option<bool> showSupportedFilesOption,
    Option<bool> showInfoOption) : BinderBase<Options>
{
    public void AddToCommand(Command command)
    {
        command.AddOption(slnOption);
        command.AddOption(uriOption);
        command.AddOption(userOption);
        command.AddOption(passOption);
        command.AddOption(noKeyOption);
        command.AddOption(diffBaseOption);
        command.AddOption(batchSizeOption);
        command.AddOption(databaseOption);
        command.AddOption(minAccessibilityOption);
        command.AddOption(logLevelOption);
        command.AddOption(debugOption);
        command.AddOption(verboseOption);
        command.AddOption(quietOption);
        command.AddOption(skipDependenciesOption);
        command.AddOption(purgeDataOption);
        command.AddOption(includeExtensionsOption);
        command.AddOption(showVersionOption);
        command.AddOption(showSupportedFilesOption);
        command.AddOption(showInfoOption);

        command.AddValidator(result => OptionsBinderValidator.Validate(
            result,
            slnOption,
            noKeyOption,
            logLevelOption,
            debugOption,
            verboseOption,
            quietOption,
            purgeDataOption,
            skipDependenciesOption,
            minAccessibilityOption,
            passOption,
            showVersionOption,
            showSupportedFilesOption,
            showInfoOption));
    }

    protected override Options GetBoundValue(BindingContext bindingContext) =>
        new(
            bindingContext.ParseResult.GetValueForOption(slnOption)!,
            bindingContext.ParseResult.GetValueForOption(uriOption)!,
            bindingContext.ParseResult.GetValueForOption(userOption)!,
            bindingContext.ParseResult.GetValueForOption(passOption),
            bindingContext.ParseResult.GetValueForOption(noKeyOption),
            bindingContext.ParseResult.GetValueForOption(diffBaseOption),
            bindingContext.ParseResult.GetValueForOption(batchSizeOption),
            bindingContext.ParseResult.GetValueForOption(databaseOption)!,
            ParseLogLevel(bindingContext),
            bindingContext.ParseResult.GetValueForOption(skipDependenciesOption),
            bindingContext.ParseResult.GetValueForOption(minAccessibilityOption),
            bindingContext.ParseResult.GetValueForOption(includeExtensionsOption)!,
            bindingContext.ParseResult.GetValueForOption(purgeDataOption),
            bindingContext.ParseResult.GetValueForOption(showVersionOption),
            bindingContext.ParseResult.GetValueForOption(showSupportedFilesOption),
            bindingContext.ParseResult.GetValueForOption(showInfoOption)
        );

    private LogLevel ParseLogLevel(BindingContext bindingContext)
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

        return logLevel;
    }
}
