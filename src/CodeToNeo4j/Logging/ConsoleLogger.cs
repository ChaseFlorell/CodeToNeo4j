using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Logging;

public class ConsoleLogger(
	string name,
	LogLevel minLogLevel) : ILogger
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
		var isProgress = message.Contains("[Progress");
		var threadId = Environment.CurrentManagedThreadId;
		var threadTag = threadId == MainThreadId
			? $"[MAIN-{threadId:D3}]"
			: Thread.CurrentThread.IsThreadPoolThread
				? $"[TASK-{threadId:D3}]"
				: $"[FINL-{threadId:D3}]";

		if (!IsRunningOnCI(message))
		{
			var timestamp = DateTime.Now.ToString("HH:mm:ss");
			var logLevelString = logLevel.Truncate();
			message = $"{timestamp} {logLevelString}{threadTag} {name} {message}";
		}
		else
		{
			message = $"{name}{threadTag} {message}";
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

	private static readonly int MainThreadId = Environment.CurrentManagedThreadId;

	private static bool IsRunningOnCI(string message) =>
		message.StartsWith("::")
		|| message.StartsWith("##vso");
}
