using CodeToNeo4j.Configuration;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Configuration;

public class ConfigurationServiceTests
{
	[Theory]
	[InlineData("CSharpHandler", ".cs", "csharp", "dotnet")]
	[InlineData("RazorHandler", ".razor", "csharp", "dotnet")]
	[InlineData("TypeScriptHandler", ".ts", "typescript", "node")]
	[InlineData("JavaScriptHandler", ".js", "javascript", "node")]
	[InlineData("CssHandler", ".css", "css", "web")]
	[InlineData("HtmlHandler", ".html", "html", "web")]
	[InlineData("XamlHandler", ".xaml", "xaml", "dotnet")]
	[InlineData("XmlHandler", ".xml", "xml", "xml")]
	[InlineData("JsonHandler", ".json", "json", "json")]
	[InlineData("DartHandler", ".dart", "dart", "flutter")]
	[InlineData("CsprojHandler", ".csproj", "xml", "dotnet")]
	[InlineData("PackageJsonHandler", "package.json", "json", "node")]
	[InlineData("PubspecYamlHandler", "pubspec.yaml", "yaml", "flutter")]
	public void GivenHandlerName_WhenGetHandlerConfigurationCalled_ThenReturnsExpectedLanguageAndExtension(
		string handlerName, string expectedFirstExtension, string expectedLanguage, string expectedTechnology)
	{
		IConfigurationService sut = ConfigurationServiceFactory.Create();

		HandlerConfiguration config = sut.GetHandlerConfiguration(handlerName);

		config.Language.ShouldBe(expectedLanguage);
		config.Technology.ShouldBe(expectedTechnology);
		config.FileExtensions.ShouldContain(expectedFirstExtension);
	}

	[Fact]
	public void GivenUnknownHandlerName_WhenGetHandlerConfigurationCalled_ThenReturnsUnknownLanguage()
	{
		IConfigurationService sut = ConfigurationServiceFactory.Create();

		HandlerConfiguration config = sut.GetHandlerConfiguration("NonExistentHandler");

		config.Language.ShouldBe("unknown");
		config.Technology.ShouldBe("unknown");
		config.FileExtensions.ShouldBeEmpty();
	}

	[Fact]
	public void GivenTypeScriptHandler_WhenGetHandlerConfigurationCalled_ThenReturnsBothExtensions()
	{
		IConfigurationService sut = ConfigurationServiceFactory.Create();

		HandlerConfiguration config = sut.GetHandlerConfiguration("TypeScriptHandler");

		config.FileExtensions.ShouldContain(".ts");
		config.FileExtensions.ShouldContain(".tsx");
	}

	[Fact]
	public void GivenHandlerNameWithDifferentCasing_WhenGetHandlerConfigurationCalled_ThenReturnsConfig()
	{
		IConfigurationService sut = ConfigurationServiceFactory.Create();

		HandlerConfiguration config = sut.GetHandlerConfiguration("csHARPhandler");

		config.Language.ShouldBe("csharp");
	}
}
