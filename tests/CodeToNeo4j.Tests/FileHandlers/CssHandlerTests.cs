using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Tests.Configuration;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CssHandlerTests
{
	[Fact]
	public async Task GivenCssWithSelectors_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		CssHandler sut = new(fileSystem, new TextSymbolMapper(), ConfigurationServiceFactory.Create());
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
		CssHandler sut = new(fileSystem, new TextSymbolMapper(), ConfigurationServiceFactory.Create());
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
		CssHandler sut = new(fileSystem, new TextSymbolMapper(), ConfigurationServiceFactory.Create());
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
}
