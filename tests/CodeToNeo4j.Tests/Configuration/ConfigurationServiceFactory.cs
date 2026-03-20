using CodeToNeo4j.Configuration;
using Microsoft.Extensions.Options;

namespace CodeToNeo4j.Tests.Configuration;

/// <summary>
/// Creates a <see cref="ConfigurationService"/> populated with all well-known handler configurations
/// for use in unit tests.
/// </summary>
internal static class ConfigurationServiceFactory
{
	internal static IConfigurationService Create()
	{
		HandlersConfiguration config = new();
		config.Handlers["CSharpHandler"] = new(".cs", "csharp");
		config.Handlers["RazorHandler"] = new(".razor", "csharp");
		config.Handlers["TypeScriptHandler"] = new(".ts", "typescript", "TypeScript");
		config.Handlers["JavaScriptHandler"] = new(".js", "javascript", "JavaScript");
		config.Handlers["CssHandler"] = new(".css", "css");
		config.Handlers["HtmlHandler"] = new(".html", "html");
		config.Handlers["XamlHandler"] = new(".xaml", "xaml");
		config.Handlers["XmlHandler"] = new(".xml", "xml");
		config.Handlers["JsonHandler"] = new(".json", "json");
		config.Handlers["DartHandler"] = new(".dart", "dart");
		config.Handlers["CsprojHandler"] = new(".csproj", "xml");
		config.Handlers["PackageJsonHandler"] = new("package.json", "json");
		config.Handlers["PubspecYamlHandler"] = new("pubspec.yaml", "yaml");
		return new ConfigurationService(Options.Create(config));
	}
}
