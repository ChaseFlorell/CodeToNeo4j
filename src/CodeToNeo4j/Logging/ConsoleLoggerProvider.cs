using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Logging;

public sealed class ConsoleLoggerProvider(LogLevel minLogLevel) : ILoggerProvider
{
	public ILogger CreateLogger(string categoryName) => new ConsoleLogger(categoryName, minLogLevel);

	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	~ConsoleLoggerProvider()
	{
		Dispose(false);
	}

	private static void Dispose(bool disposing)
	{
		if (disposing)
		{
			// Dispose managed resources here if any
		}

		// Dispose unmanaged resources here if any
	}
}
