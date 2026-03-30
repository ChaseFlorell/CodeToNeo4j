using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Dart.Yaml;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.Dart;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.Dart;

public class PubspecYamlHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration(["pubspec.yaml"], "yaml"));
		return fake;
	}

	[Fact]
	public async Task GivenPubspecWithDependencies_WhenHandled_ThenExtractsDependencySymbols()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		PubspecYamlHandler sut = new(fileSystem, new TextSymbolMapper(), new PubspecParser(), NullLogger<PubspecYamlHandler>.Instance, CreateConfigService());

		const string content = """
		                       name: my_app
		                       dependencies:
		                         http: ^0.13.0
		                         path: ^1.9.0
		                       dev_dependencies:
		                         mockito: ^5.0.0
		                       """;

		var filePath = "/project/pubspec.yaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"pubspec.yaml",
			filePath,
			"pubspec.yaml",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.ShouldContain(s => s.Name == "http" && s.Key == "pkg:http" && s.Kind == "Dependency");
		symbolBuffer.ShouldContain(s => s.Name == "path" && s.Key == "pkg:path" && s.Kind == "Dependency");
		symbolBuffer.ShouldContain(s => s.Name == "mockito" && s.Key == "pkg:mockito" && s.Kind == "Dependency");

		relBuffer.ShouldContain(r => r.FromKey == "pubspec.yaml" && r.ToKey == "pkg:http" && r.RelType == GraphSchema.Relationships.DependsOn);
		relBuffer.ShouldContain(r => r.FromKey == "pubspec.yaml" && r.ToKey == "pkg:path" && r.RelType == GraphSchema.Relationships.DependsOn);
		relBuffer.ShouldContain(r => r.FromKey == "pubspec.yaml" && r.ToKey == "pkg:mockito" && r.RelType == GraphSchema.Relationships.DependsOn);
	}

	[Fact]
	public async Task GivenEmptyPubspec_WhenHandled_ThenReturnsNoSymbols()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		PubspecYamlHandler sut = new(fileSystem, new TextSymbolMapper(), new PubspecParser(), NullLogger<PubspecYamlHandler>.Instance, CreateConfigService());

		const string content = "name: empty_app\n";
		var filePath = "/project/pubspec.yaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"pubspec.yaml",
			filePath,
			"pubspec.yaml",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("/project/pubspec.yaml", true)]
	[InlineData("/project/other.yaml", false)]
	[InlineData("/project/pubspec.lock", false)]
	public void GivenFilePath_WhenCheckingCanHandle_ThenReturnsExpectedResult(string filePath, bool expected)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		PubspecYamlHandler sut = new(fileSystem, new TextSymbolMapper(), new PubspecParser(), NullLogger<PubspecYamlHandler>.Instance, CreateConfigService());

		// Act & Assert
		sut.CanHandle(filePath).ShouldBe(expected);
	}

	[Fact]
	public async Task GivenDependencyWithNoVersion_WhenHandled_ThenFqnIsJustName()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		PubspecYamlHandler sut = new(fileSystem, new TextSymbolMapper(), new PubspecParser(), NullLogger<PubspecYamlHandler>.Instance, CreateConfigService());

		// Dependency with no version value (empty after colon)
		const string content = """
		                       name: test_app
		                       dependencies:
		                         flutter:
		                       """;

		var filePath = "/project/pubspec.yaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"pubspec.yaml",
			filePath,
			"pubspec.yaml",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert — dependency is present but version is null, so Fqn is just the name
		var flutterSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "flutter");
		flutterSymbol.ShouldNotBeNull();
		flutterSymbol.Version.ShouldBeNull();
		flutterSymbol.Fqn.ShouldBe("flutter");
	}

	[Fact]
	public async Task GivenMalformedPubspec_WhenHandled_ThenReturnsEmptyWithoutThrowing()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		PubspecYamlHandler sut = new(fileSystem, new TextSymbolMapper(), new PubspecParser(), NullLogger<PubspecYamlHandler>.Instance, CreateConfigService());

		const string content = "{ this is: [not valid yaml: - broken";
		var filePath = "/project/pubspec.yaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act — should not throw
		var exception = await Record.ExceptionAsync(() => sut.Handle(
			null,
			null,
			"test-repo",
			"pubspec.yaml",
			filePath,
			"pubspec.yaml",
			symbolBuffer,
			relBuffer,
			Accessibility.Private));

		// Assert
		exception.ShouldBeNull();
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenDependencyWithVersion_WhenHandled_ThenVersionIsIncluded()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		PubspecYamlHandler sut = new(fileSystem, new TextSymbolMapper(), new PubspecParser(), NullLogger<PubspecYamlHandler>.Instance, CreateConfigService());

		const string content = """
		                       name: test_app
		                       dependencies:
		                         http: ^0.13.0
		                       """;

		var filePath = "/project/pubspec.yaml";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"pubspec.yaml",
			filePath,
			"pubspec.yaml",
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		var httpSymbol = symbolBuffer.First(s => s.Name == "http");
		httpSymbol.Version.ShouldBe("^0.13.0");
		httpSymbol.Documentation.ShouldBe("^0.13.0");
		httpSymbol.Fqn.ShouldBe("http (^0.13.0)");
	}
}
