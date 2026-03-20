using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CssHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".css"], "css"));
		return fake;
	}

	[Fact]
	public async Task GivenCssWithSelectors_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CssHandler sut = new(fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = @"body { color: black; }";
		var filePath = "test.css";
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
		var symbol = symbolBuffer.FirstOrDefault(s => s.Name == "body");
		symbol.ShouldNotBeNull();
		symbol.Kind.ShouldBe("CssSelector");

		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == symbol.Key && r.RelType == "CONTAINS");
	}

	[Fact]
	public async Task GivenCssWithAtRules_WhenHandleCalled_ThenSkipsAtRules()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CssHandler sut = new(fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = @"@import ""foo.css""; @media screen { .foo { color: red; } }";
		var filePath = "test.css";
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
		symbolBuffer.ShouldNotContain(s => s.Name.StartsWith("@"));
		symbolBuffer.ShouldContain(s => s.Name == ".foo");
	}

	[Fact]
	public async Task GivenMinAccessibilityNotApplicable_WhenHandleCalled_ThenDoesNotAddSymbols()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CssHandler sut = new(fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = @".foo { color: red; }";
		var filePath = "test.css";
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
	[InlineData("@keyframes spin { from { transform: rotate(0); } }", "css3")]
	[InlineData("@supports (display: grid) { .foo { color: red; } }", "css3")]
	[InlineData(":root { --primary: blue; }", "css3")]
	[InlineData("body { color: black; font-size: 12px; }", "css2")]
	public void GivenCssContent_WhenDetectCssVersionCalled_ThenReturnsExpectedVersion(string content, string expected)
	{
		CssHandler.DetectCssVersion(content).ShouldContain(expected);
	}

	[Fact]
	public async Task GivenCss3Content_WhenHandleCalled_ThenFileResultContainsCss3TargetFramework()
	{
		MockFileSystem fileSystem = new();
		CssHandler sut = new(fileSystem, new TextSymbolMapper(), CreateConfigService());
		var content = "@keyframes spin { from { transform: rotate(0deg); } }";
		var filePath = "styles.css";
		fileSystem.AddFile(filePath, new(content));

		var result = await sut.Handle(null, null, null, "key", filePath, filePath, [], [], Accessibility.Public);

		result.TargetFrameworks.ShouldNotBeNull();
		result.TargetFrameworks.ShouldContain("css3");
	}
}
