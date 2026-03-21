using Microsoft.Extensions.Options;

namespace CodeToNeo4j.Configuration;

public class ConfigurationService(IOptions<HandlersConfiguration> options) : IConfigurationService
{
	private readonly HandlersConfiguration _config = options.Value;

	public HandlerConfiguration GetHandlerConfiguration(string handlerTypeName)
		=> _config.Handlers.TryGetValue(handlerTypeName, out var config)
			? config
			: new([], "unknown", "unknown");
}
