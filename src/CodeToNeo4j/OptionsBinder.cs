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
    Option<string> repoKeyOption,
    Option<string?> diffBaseOption,
    Option<int> batchSizeOption,
    Option<string> databaseOption,
    Option<LogLevel> logLevelOption,
    Option<bool> skipDependenciesOption,
    Option<Accessibility> minAccessibilityOption,
    Option<string[]> includeExtensionsOption) : BinderBase<Options>
{
    protected override Options GetBoundValue(BindingContext bindingContext) =>
        new(
            bindingContext.ParseResult.GetValueForOption(slnOption)!,
            bindingContext.ParseResult.GetValueForOption(uriOption)!,
            bindingContext.ParseResult.GetValueForOption(userOption)!,
            bindingContext.ParseResult.GetValueForOption(passOption)!,
            bindingContext.ParseResult.GetValueForOption(repoKeyOption)!,
            bindingContext.ParseResult.GetValueForOption(diffBaseOption),
            bindingContext.ParseResult.GetValueForOption(batchSizeOption),
            bindingContext.ParseResult.GetValueForOption(databaseOption)!,
            bindingContext.ParseResult.GetValueForOption(logLevelOption),
            bindingContext.ParseResult.GetValueForOption(skipDependenciesOption),
            bindingContext.ParseResult.GetValueForOption(minAccessibilityOption),
            bindingContext.ParseResult.GetValueForOption(includeExtensionsOption)!
        );
}