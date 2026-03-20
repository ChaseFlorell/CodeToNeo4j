using CodeToNeo4j.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CodeToNeo4j.Tests.Configuration;

/// <summary>
/// Loads the shared <c>config.json</c> from the test output directory and returns a fully
/// configured <see cref="IConfigurationService"/> for use in unit tests.
/// </summary>
internal static class ConfigurationServiceFactory
{
	internal static IConfigurationService Create()
	{
		var configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("config.json", optional: false)
			.Build();

		HandlersConfiguration handlers = new();
		configuration.Bind(handlers);
		return new ConfigurationService(Options.Create(handlers));
	}
}
