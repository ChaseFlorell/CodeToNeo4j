using System.CommandLine;
using System.CommandLine.Binding;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Console;

public class OptionsBinder(
    Option<FileInfo> slnOption,
    Option<string> neo4JOption,
    Option<string> userOption,
    Option<string> passOption,
    Option<string> repoKeyOption,
    Option<string?> diffBaseOption,
    Option<int> batchSizeOption,
    Option<string> databaseOption,
    Option<LogLevel> logLevelOption) : BinderBase<Options>
{
    protected override Options GetBoundValue(BindingContext bindingContext) =>
        new(
            bindingContext.ParseResult.GetValueForOption(slnOption)!,
            bindingContext.ParseResult.GetValueForOption(neo4JOption)!,
            bindingContext.ParseResult.GetValueForOption(userOption)!,
            bindingContext.ParseResult.GetValueForOption(passOption)!,
            bindingContext.ParseResult.GetValueForOption(repoKeyOption)!,
            bindingContext.ParseResult.GetValueForOption(diffBaseOption),
            bindingContext.ParseResult.GetValueForOption(batchSizeOption),
            bindingContext.ParseResult.GetValueForOption(databaseOption)!,
            bindingContext.ParseResult.GetValueForOption(logLevelOption)
        );
}