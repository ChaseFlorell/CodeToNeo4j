using CodeToNeo4j.Configuration;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Configuration;

public class ConfigurationServiceTests
{
	[Theory]
	[InlineData("CSharpHandler", ".cs", "csharp")]
	[InlineData("RazorHandler", ".razor", "csharp")]
	[InlineData("TypeScriptHandler", ".ts", "typescript")]
	[InlineData("JavaScriptHandler", ".js", "javascript")]
	[InlineData("CssHandler", ".css", "css")]
	[InlineData("HtmlHandler", ".html", "html")]
	[InlineData("XamlHandler", ".xaml", "xaml")]
	[InlineData("XmlHandler", ".xml", "xml")]
	[InlineData("JsonHandler", ".json", "json")]
	[InlineData("DartHandler", ".dart", "dart")]
	[InlineData("CsprojHandler", ".csproj", "xml")]
	[InlineData("PackageJsonHandler", "package.json", "json")]
	[InlineData("PubspecYamlHandler", "pubspec.yaml", "yaml")]
	public void GivenHandlerName_WhenGetHandlerConfigurationCalled_ThenReturnsExpectedLanguageAndExtension(
		string handlerName, string expectedFirstExtension, string expectedLanguage)
	{
		IConfigurationService sut = ConfigurationServiceFactory.Create();

		HandlerConfiguration config = sut.GetHandlerConfiguration(handlerName);

		config.Language.ShouldBe(expectedLanguage);
		config.FileExtensions.ShouldContain(expectedFirstExtension);
	}

	[Fact]
	public void GivenUnknownHandlerName_WhenGetHandlerConfigurationCalled_ThenReturnsUnknownLanguage()
	{
		IConfigurationService sut = ConfigurationServiceFactory.Create();

		HandlerConfiguration config = sut.GetHandlerConfiguration("NonExistentHandler");

		config.Language.ShouldBe("unknown");
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
