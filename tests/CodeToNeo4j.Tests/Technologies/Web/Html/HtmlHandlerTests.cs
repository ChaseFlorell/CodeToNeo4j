using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.Web.Html;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.Web.Html;

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

		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == scriptSymbol.Key && r.RelType == GraphSchema.Relationships.DependsOn);
		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == idSymbol.Key && r.RelType == GraphSchema.Relationships.Contains);
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

}
