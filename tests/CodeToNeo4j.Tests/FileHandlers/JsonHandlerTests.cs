using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using CodeToNeo4j.Configuration;

namespace CodeToNeo4j.Tests.FileHandlers;

public class JsonHandlerTests
{
	[Fact]
	public async Task GivenJsonWithNestedProperties_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var logger = A.Fake<ILogger<JsonHandler>>();
		JsonHandler sut = new(fileSystem, logger, new TextSymbolMapper(), new ConfigurationService());
		const string content = @"{ ""foo"": { ""bar"": 123 }, ""baz"": [1, 2] }";
		const string filePath = "test.json";
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
		var fooSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "foo");
		fooSymbol.ShouldNotBeNull();
		fooSymbol.Fqn.ShouldBe("foo");
		fooSymbol.Class.ShouldBe("property");

		var barSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "bar");
		barSymbol.ShouldNotBeNull();
		barSymbol.Fqn.ShouldBe("foo.bar");
		barSymbol.Class.ShouldBe("property");

		var bazSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "baz");
		bazSymbol.ShouldNotBeNull();
		bazSymbol.Fqn.ShouldBe("baz");
		bazSymbol.Class.ShouldBe("property");

		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == fooSymbol.Key && r.RelType == "CONTAINS");
		relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == bazSymbol.Key && r.RelType == "CONTAINS");
	}

	[Fact]
	public async Task GivenMalformedJson_WhenHandleCalled_ThenLogsWarning()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var logger = A.Fake<ILogger<JsonHandler>>();
		JsonHandler sut = new(fileSystem, logger, new TextSymbolMapper(), new ConfigurationService());
		const string content = @"{ ""foo"": }"; // Invalid JSON
		const string filePath = "test.json";
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
		symbolBuffer.ShouldBeEmpty();
		A.CallTo(logger).Where(call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0]! == LogLevel.Warning).MustHaveHappened();
	}

	[Fact]
	public async Task GivenMinAccessibilityNotApplicable_WhenHandleCalled_ThenDoesNotAddSymbols()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var logger = A.Fake<ILogger<JsonHandler>>();
		JsonHandler sut = new(fileSystem, logger, new TextSymbolMapper(), new ConfigurationService());
		const string content = @"{ ""foo"": 1 }";
		const string filePath = "test.json";
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
	}

	[Fact]
	public async Task GivenArrayOfObjects_WhenHandleCalled_ThenProcessesObjects()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var logger = A.Fake<ILogger<JsonHandler>>();
		JsonHandler sut = new(fileSystem, logger, new TextSymbolMapper(), new ConfigurationService());
		const string content = @"[ { ""foo"": 1 }, { ""bar"": 2 } ]";
		const string filePath = "test.json";
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
		symbolBuffer.ShouldContain(s => s.Name == "foo" && s.Fqn == "[0].foo");
		symbolBuffer.ShouldContain(s => s.Name == "bar" && s.Fqn == "[1].bar");
	}
}
