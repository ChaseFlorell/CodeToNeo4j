using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Logging;

public static class LoggingExtensions
{
	public static string Truncate(this LogLevel logLevel) =>
		logLevel switch
		{
			LogLevel.None => "[NONE]",
			LogLevel.Trace => "[VERB]",
			LogLevel.Debug => "[DEBUG]",
			LogLevel.Information => "[INFO]",
			LogLevel.Warning => "[WARN]",
			LogLevel.Error => "[ERR]",
			LogLevel.Critical => "[CRIT]",
			_ => $"[{logLevel.ToString()[..3].ToUpper()}]"
		};
}
