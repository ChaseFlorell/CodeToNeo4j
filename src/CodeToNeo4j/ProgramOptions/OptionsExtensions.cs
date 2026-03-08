using System.CommandLine;

namespace CodeToNeo4j.ProgramOptions;

public static class OptionsExtensions
{
    public static Option<T> WithAlias<T>(this Option<T> option, string alias)
    {
        option.AddAlias(alias);
        return option;
    }

    public static Option<T> IsRequired<T>(this Option<T> option)
    {
        option.IsRequired = true;
        return option;
    }

    public static Option<T> WithArgumentHelpName<T>(this Option<T> option, string argumentHelpName)
    {
        option.ArgumentHelpName = argumentHelpName;
        return option;
    }

    public static Option<T> WithDescription<T>(this Option<T> option, string description)
    {
        option.Description = description;
        return option;
    }

    public static Option<T> WithDefaultValueFunc<T>(this Option<T> option, Func<T> defaultValue)
    {
        option.SetDefaultValueFactory(() => defaultValue());
        return option;
    }

    public static IOptionsHandler BuildChain(this IEnumerable<IOptionsHandler> handlers)
    {
        var optionsHandlers = handlers.ToList();
        optionsHandlers.Aggregate((current, next) =>
        {
            current.SetNext(next);
            return next;
        });

        return optionsHandlers[0];
    }
}