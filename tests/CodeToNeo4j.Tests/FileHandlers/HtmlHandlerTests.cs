using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class HtmlHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".html"], "html"));
		return fake;
	}

	[Fact]
	public async Task GivenHtmlWithScriptAndId_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		HtmlHandler sut = new(fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = @"
<html>
  <head>
    <script src=""app.js""></script>
  </head>
  <body>
    <div id=""myId""></div>
  </body>
</html>";
		var filePath = "test.html";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var scriptSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "HtmlScriptReference");
		scriptSymbol.ShouldNotBeNull();
		scriptSymbol.Name.ShouldBe("app.js");

		var idSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "HtmlElementId");
		idSymbol.ShouldNotBeNull();
		idSymbol.Name.ShouldBe("myId");

		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == scriptSymbol.Key && r.RelType == "DEPENDS_ON");
		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == idSymbol.Key && r.RelType == "CONTAINS");
	}

	[Fact]
	public async Task GivenMinAccessibilityNotApplicable_WhenHandleCalled_ThenDoesNotAddSymbols()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		HtmlHandler sut = new(fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = @"<div id=""myId""></div><script src=""app.js""></script>";
		var filePath = "test.html";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"test-file",
			filePath, filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("<!DOCTYPE html><html></html>", "html5")]
	[InlineData("<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.01//EN\"><html></html>", "html4.01")]
	[InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\"><html></html>", "xhtml1.0")]
	[InlineData("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.1//EN\"><html></html>", "xhtml1.1")]
	[InlineData("<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 3.2 Final//EN\"><html></html>", "html3.2")]
	[InlineData("<html></html>", "html5")]
	public void GivenHtmlContent_WhenDetectHtmlVersionCalled_ThenReturnsExpectedVersion(string content, string expected)
	{
		var result = HtmlHandler.DetectHtmlVersion(content);
		result.ShouldContain(expected);
	}

	[Fact]
	public async Task GivenHtml5Page_WhenHandleCalled_ThenFileResultContainsHtml5TargetFramework()
	{
		MockFileSystem fileSystem = new();
		HtmlHandler sut = new(fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = "<!DOCTYPE html><html><body></body></html>";
		var filePath = "index.html";
		fileSystem.AddFile(filePath, new(content));

		var result = await sut.Handle(null, null, null, "key", filePath, filePath, [], [], Accessibility.Public);

		result.TargetFrameworks.ShouldNotBeNull();
		result.TargetFrameworks.ShouldContain("html5");
	}
}
