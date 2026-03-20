namespace CodeToNeo4j.Configuration;

public class HandlersConfiguration
{
	public Dictionary<string, HandlerConfiguration> Handlers { get; } = new(StringComparer.OrdinalIgnoreCase);
}
