using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Logging;

public class ConsoleLoggerProvider(LogLevel minLogLevel) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new ConsoleLogger(categoryName, minLogLevel);
    public void Dispose() { }
}
