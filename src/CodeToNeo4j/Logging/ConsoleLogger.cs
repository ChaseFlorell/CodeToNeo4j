using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Logging;

public class ConsoleLogger(string name, LogLevel minLogLevel) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minLogLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var logLevelString = logLevel.Truncate();

        if (logLevel == LogLevel.Information && message.StartsWith("[Progress:"))
        {
            Console.Write($"\r{timestamp} {logLevelString} {name}[{eventId.Id}] {message}");
            return;
        }

        Console.WriteLine($"{timestamp} {logLevelString} {name}[{eventId.Id}] {message}");

        if (exception != null)
        {
            Console.Error.WriteLine(exception.ToString());
        }
    }
}