using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace CodeToNeo4j.Progress;

/// <summary>
/// Default progress reporting for local command line environments using Spectre.Console.
/// </summary>
public class ConsoleProgressService(LogLevel minLogLevel) : IProgressService
{
    public void ReportProgress(int current, int total, string filePath)
    {
        if (minLogLevel >= LogLevel.Warning)
        {
            return;
        }

        var percentage = (double)current / total * 100;
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logName = typeof(ConsoleProgressService).FullName ?? "CodeToNeo4j.Progress.ConsoleProgressService";

        if (minLogLevel <= LogLevel.Debug)
        {
            // Debug or Trace (Verbose) - multi-line behavior
            AnsiConsole.MarkupLine($"[grey]{timestamp} {minLogLevel.ToString().ToUpper()} {Markup.Escape(logName)}[0] [[Progress: {percentage:F2} %]] [[{current}/{total}]][/]");
        }
        else
        {
            // Information - single-line behavior
            AnsiConsole.Markup($"\r[white]{timestamp} INFO {Markup.Escape(logName)}[0] [[Progress: {percentage:F2} %]] [[{current}/{total}]][/]");

            if (current == total)
            {
                AnsiConsole.WriteLine();
            }
        }
    }
}
