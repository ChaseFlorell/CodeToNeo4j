using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Logging;

public class ConsoleLogger(string name, LogLevel minLogLevel) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= minLogLevel;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        var isProgress = message.StartsWith("::progress");

        if (!message.StartsWith("::") && !message.StartsWith("##vso"))
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var logLevelString = logLevel.Truncate();
            message = $"{timestamp} {logLevelString} {name}[{eventId.Id}] {message}";
        }
        else
        {
            message = $"{name}[{eventId.Id}] {message}";
        }


        if (logLevel == LogLevel.Information && isProgress)
        {
            Console.Write($"\r{message}");
            return;
        }

        Console.WriteLine($"{message}");

        if (exception != null)
        {
            Console.Error.WriteLine(exception.ToString());
        }
    }
}