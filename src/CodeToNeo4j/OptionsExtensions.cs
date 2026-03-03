using System.CommandLine;

namespace CodeToNeo4j;

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
}