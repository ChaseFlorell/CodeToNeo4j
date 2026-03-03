using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace CodeToNeo4j.Logging;

public class SpectreConsoleLogger(string name, LogLevel minLogLevel) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minLogLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);

        var color = logLevel switch
        {
            LogLevel.Trace => "grey",
            LogLevel.Debug => "grey",
            LogLevel.Information => "white",
            LogLevel.Warning => "yellow",
            LogLevel.Error => "red",
            LogLevel.Critical => "red",
            _ => "white"
        };

        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        AnsiConsole.MarkupLine($"[{color}]{timestamp} {logLevel.ToString().ToUpper()} {Markup.Escape(name)}[{eventId.Id}] {Markup.Escape(message)}[/]");

        if (exception != null)
        {
            AnsiConsole.WriteException(exception);
        }
    }
}