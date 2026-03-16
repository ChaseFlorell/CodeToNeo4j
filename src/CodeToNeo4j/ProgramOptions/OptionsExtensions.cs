using System.CommandLine;
using CodeToNeo4j.ProgramOptions.Handlers;

namespace CodeToNeo4j.ProgramOptions;

public static class OptionsExtensions
{
    extension<T>(Option<T> option)
    {
        public Option<T> WithAlias(string alias)
        {
            option.Aliases.Add(alias);
            return option;
        }

        public Option<T> IsRequired()
        {
            option.Required = true;
            return option;
        }

        public Option<T> WithArgumentHelpName(string argumentHelpName)
        {
            option.HelpName = argumentHelpName;
            return option;
        }

        public Option<T> WithDescription(string description)
        {
            option.Description = description;
            return option;
        }

        public Option<T> WithDefaultValueFunc(Func<T> defaultValue)
        {
            option.DefaultValueFactory = _ => defaultValue();
            return option;
        }
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
