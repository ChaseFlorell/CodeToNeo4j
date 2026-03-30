using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Dart.Bridge;
using CodeToNeo4j.Dart.Models;
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

public class DartHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".dart"], "dart"));
		return fake;
	}

	[Fact]
	public async Task GivenDartFile_WhenBridgeReturnsResult_ThenExtractsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var bridgeService = A.Fake<IDartBridgeService>();
		DartHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance, CreateConfigService());

		var projectRoot = "/project";
		fileSystem.AddFile("/project/pubspec.yaml", new("name: test_app"));
		fileSystem.AddFile("/project/lib/src/foo.dart", new("class Foo {}"));

		DartAnalysisResult analysisResult = new()
		{
			ProjectName = "test_app",
			ProjectRoot = projectRoot,
			Files = new()
			{
				["lib/src/foo.dart"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "Foo",
							Kind = "DartClass",
							Class = "class",
							Fqn = "package:test_app/src/foo.dart::Foo",
							Accessibility = "Public",
							StartLine = 1,
							EndLine = 1,
							Namespace = "package:test_app/lib/src"
						}
					],
					Relationships =
					[
						new()
						{
							FromSymbol = "Foo",
							FromKind = "class",
							FromLine = 1,
							ToSymbol = "Bar",
							ToKind = "class",
							RelType = GraphSchema.Relationships.DependsOn
						}
					]
				}
			}
		};

		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"lib/src/foo.dart",
			"/project/lib/src/foo.dart",
			"lib/src/foo.dart",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldContain(s => s.Name == "Foo" && s.Kind == "DartClass");
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.DependsOn);
	}

	[Fact]
	public async Task GivenDartFile_WhenBridgeReturnsNull_ThenReturnsEmptyResult()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var bridgeService = A.Fake<IDartBridgeService>();
		DartHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/pubspec.yaml", new("name: test_app"));
		fileSystem.AddFile("/project/lib/main.dart", new("void main() {}"));

		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns((DartAnalysisResult?)null);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"lib/main.dart",
			"/project/lib/main.dart",
			"lib/main.dart",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenDartFile_WhenNoPubspecFound_ThenReturnsEmptyResult()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var bridgeService = A.Fake<IDartBridgeService>();
		DartHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/orphan/main.dart", new("void main() {}"));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"orphan/main.dart",
			"/orphan/main.dart",
			"orphan/main.dart",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenFileNotInAnalysisResults_WhenHandled_ThenReturnsEmptyResult()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var bridgeService = A.Fake<IDartBridgeService>();
		DartHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/pubspec.yaml", new("name: test_app"));
		fileSystem.AddFile("/project/lib/other.dart", new("class Other {}"));

		DartAnalysisResult analysisResult = new()
		{
			ProjectName = "test_app",
			ProjectRoot = "/project",
			Files = new() { ["lib/different_file.dart"] = new() { Symbols = [], Relationships = [] } }
		};
		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"lib/other.dart",
			"/project/lib/other.dart",
			"lib/other.dart",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert — file wasn't in analysis results, so nothing is emitted
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenFileInRootDirectory_WhenHandled_ThenNamespaceIsNull()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var bridgeService = A.Fake<IDartBridgeService>();
		DartHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance, CreateConfigService());

		// File at project root — no parent directory → fileNamespace is null
		fileSystem.AddFile("/project/pubspec.yaml", new("name: test_app"));
		fileSystem.AddFile("/project/root.dart", new("class Root {}"));

		DartAnalysisResult analysisResult = new()
		{
			ProjectName = "test_app",
			ProjectRoot = "/project",
			Files = new()
			{
				["root.dart"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "Root",
							Kind = "DartClass",
							Class = "class",
							Fqn = "package:test_app/root.dart::Root",
							Accessibility = "Public",
							StartLine = 1,
							EndLine = 1,
							Namespace = null // forces fileNamespace fallback
						}
					],
					Relationships = []
				}
			}
		};
		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act — relativePath has no directory component
		await sut.Handle(
			null,
			null,
			"test-repo",
			"root.dart",
			"/project/root.dart",
			"root.dart",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert — symbol present; namespace falls back to null/empty fileNamespace
		symbolBuffer.ShouldContain(s => s.Name == "Root");
		symbolBuffer.First(s => s.Name == "Root").Namespace.ShouldBeNullOrEmpty();
	}

	[Theory]
	[InlineData("Protected", Accessibility.Public, 0)]
	[InlineData("Internal", Accessibility.Public, 0)]
	[InlineData("Protected", Accessibility.Protected, 1)]
	[InlineData("Internal", Accessibility.Internal, 1)]
	[InlineData("UnknownAccess", Accessibility.Public, 1)]
	public async Task GivenSymbolAccessibility_WhenMinAccessibilityFilters_ThenIncludesOrExcludesCorrectly(
		string symbolAccessibility, Accessibility minAccessibility, int expectedCount)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var bridgeService = A.Fake<IDartBridgeService>();
		DartHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/pubspec.yaml", new("name: test_app"));
		fileSystem.AddFile("/project/lib/foo.dart", new("class Foo {}"));

		DartAnalysisResult analysisResult = new()
		{
			ProjectName = "test_app",
			ProjectRoot = "/project",
			Files = new()
			{
				["lib/foo.dart"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "Foo",
							Kind = "DartClass",
							Class = "class",
							Fqn = "package:test_app/foo.dart::Foo",
							Accessibility = symbolAccessibility,
							StartLine = 1,
							EndLine = 1
						}
					],
					Relationships = []
				}
			}
		};

		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"lib/foo.dart",
			"/project/lib/foo.dart",
			"lib/foo.dart",
			symbolBuffer,
			relBuffer,
			minAccessibility);

		// Assert
		symbolBuffer.Count.ShouldBe(expectedCount);
	}

	[Theory]
	[InlineData(Accessibility.Public, 1)]
	[InlineData(Accessibility.Private, 1)]
	[InlineData(Accessibility.NotApplicable, 1)]
	public async Task GivenDartFile_WhenMinAccessibilitySet_ThenFiltersCorrectly(Accessibility minAccessibility, int expectedSymbolCount)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var bridgeService = A.Fake<IDartBridgeService>();
		DartHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/pubspec.yaml", new("name: test_app"));
		fileSystem.AddFile("/project/lib/foo.dart", new("class Foo {}"));

		DartAnalysisResult analysisResult = new()
		{
			ProjectName = "test_app",
			ProjectRoot = "/project",
			Files = new()
			{
				["lib/foo.dart"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "Foo",
							Kind = "DartClass",
							Class = "class",
							Fqn = "package:test_app/foo.dart::Foo",
							Accessibility = "Public",
							StartLine = 1,
							EndLine = 1
						}
					],
					Relationships = []
				}
			}
		};

		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"lib/foo.dart",
			"/project/lib/foo.dart",
			"lib/foo.dart",
			symbolBuffer,
			relBuffer,
			minAccessibility);

		// Assert
		symbolBuffer.Count.ShouldBe(expectedSymbolCount);
	}
}
