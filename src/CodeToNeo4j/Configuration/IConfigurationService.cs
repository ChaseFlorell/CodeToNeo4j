namespace CodeToNeo4j.Configuration;

public interface IConfigurationService
{
	HandlerConfiguration GetHandlerConfiguration(string handlerTypeName);
}
