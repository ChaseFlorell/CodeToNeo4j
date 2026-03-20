using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeToNeo4j.Configuration;

public class ConfigurationService : IConfigurationService
{
	private readonly Dictionary<string, HandlerConfiguration> _handlers;

	public ConfigurationService()
	{
		using var stream = typeof(ConfigurationService).Assembly
			.GetManifestResourceStream("CodeToNeo4j.Configuration.handlers.json")!;

		var settings = JsonSerializer.Deserialize<HandlersRoot>(stream)!;
		_handlers = new(settings.Handlers, StringComparer.OrdinalIgnoreCase);
	}

	public HandlerConfiguration GetHandlerConfiguration(string handlerTypeName)
		=> _handlers.TryGetValue(handlerTypeName, out var config)
			? config
			: new("", "unknown");

	private sealed record HandlersRoot(
		[property: JsonPropertyName("handlers")] Dictionary<string, HandlerConfiguration> Handlers);
}
