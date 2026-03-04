using System.CommandLine;
using System.CommandLine.Binding;
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
    Option<string[]> includeExtensionsOption) : BinderBase<Options>
{
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
            bindingContext.ParseResult.GetValueForOption(purgeDataOption)
        );
    }
}