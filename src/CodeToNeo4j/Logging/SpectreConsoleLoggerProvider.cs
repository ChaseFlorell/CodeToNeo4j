using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Logging;

public class SpectreConsoleLoggerProvider(LogLevel minLogLevel) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new SpectreConsoleLogger(categoryName, minLogLevel);
    public void Dispose() { }
}