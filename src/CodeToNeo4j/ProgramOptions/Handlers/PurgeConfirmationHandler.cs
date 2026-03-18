using System.Diagnostics.CodeAnalysis;

namespace CodeToNeo4j.ProgramOptions.Handlers;

[ExcludeFromCodeCoverage(Justification = "Reads interactive Console.ReadKey — requires a live terminal, not unit testable")]
public class PurgeConfirmationHandler : OptionsHandler
{
    protected override Task<bool> HandleOptions(Options options)
    {
        if (options.PurgeData)
        {
            var purgeTarget = options.RepoKey is null ? "ALL CodeToNeo4j data" : $"all data for repository key '{options.RepoKey}'";
            Console.Write($"Are you sure you want to purge {purgeTarget}? (y/n): ");
            var response = Console.ReadKey();
            Console.WriteLine(); // Ensure newline after key press
            if (response.Key != ConsoleKey.Y)
            {
                Console.WriteLine("Purge aborted.");
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }
}