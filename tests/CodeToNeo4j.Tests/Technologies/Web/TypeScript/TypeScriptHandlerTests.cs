using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.Web.TypeScript;
using CodeToNeo4j.TypeScript.Bridge;
using CodeToNeo4j.TypeScript.Models;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.Web.TypeScript;

public class TypeScriptHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".ts", ".tsx"], "typescript", "node", KindPrefix: "TypeScript"));
		return fake;
	}

	[Theory]
	[InlineData("test.ts")]
	[InlineData("test.tsx")]
	public void GivenTsOrTsxFile_WhenCanHandleCalled_ThenReturnsTrue(string fileName)
	{
		TypeScriptHandler sut = new(
			new MockFileSystem(),
			new TextSymbolMapper(),
			A.Fake<ITypeScriptBridgeService>(),
			NullLogger<TypeScriptHandler>.Instance,
			CreateConfigService());

		sut.CanHandle(fileName).ShouldBeTrue();
	}

	[Fact]
	public void GivenJsFile_WhenCanHandleCalled_ThenReturnsFalse()
	{
		TypeScriptHandler sut = new(
			new MockFileSystem(),
			new TextSymbolMapper(),
			A.Fake<ITypeScriptBridgeService>(),
			NullLogger<TypeScriptHandler>.Instance,
			CreateConfigService());

		sut.CanHandle("test.js").ShouldBeFalse();
	}

	[Fact]
	public async Task GivenTsFile_WhenBridgeReturnsResult_ThenExtractsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<TypeScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/foo.ts", new("export class Foo {}"));

		TsAnalysisResult analysisResult = new()
		{
			ProjectName = "my-app",
			ProjectRoot = "/project",
			Files = new()
			{
				["src/foo.ts"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "Foo",
							Kind = "TypeScriptClass",
							Class = "class",
							Fqn = "@my-app/src/foo.ts::Foo",
							Accessibility = "Public",
							StartLine = 1,
							EndLine = 1,
							Namespace = "@my-app/src"
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
			"src/foo.ts",
			"/project/src/foo.ts",
			"src/foo.ts",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldContain(s => s.Name == "Foo" && s.Kind == "TypeScriptClass");
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.DependsOn);
	}

	[Fact]
	public async Task GivenTsFile_WhenBridgeReturnsNull_ThenReturnsEmptyResult()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<TypeScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/foo.ts", new("export class Foo {}"));

		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns((TsAnalysisResult?)null);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"src/foo.ts",
			"/project/src/foo.ts",
			"src/foo.ts",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenNoPackageJsonFound_WhenHandled_ThenSkipsBridgeAndReturnsEmpty()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<TypeScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/orphan/foo.ts", new("export class Foo {}"));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"orphan/foo.ts",
			"/orphan/foo.ts",
			"orphan/foo.ts",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenFileNotInBridgeResult_WhenHandled_ThenReturnsEmptyResult()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<TypeScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/foo.ts", new(""));

		TsAnalysisResult analysisResult = new()
		{
			ProjectName = "my-app",
			ProjectRoot = "/project",
			Files = new() { ["src/other.ts"] = new() { Symbols = [], Relationships = [] } }
		};
		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"src/foo.ts",
			"/project/src/foo.ts",
			"src/foo.ts",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenFileInRootDirectory_WhenHandled_ThenNamespaceIsNullOrEmpty()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<TypeScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/root.ts", new("export class Root {}"));

		TsAnalysisResult analysisResult = new()
		{
			ProjectName = "my-app",
			ProjectRoot = "/project",
			Files = new()
			{
				["root.ts"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "Root",
							Kind = "TypeScriptClass",
							Class = "class",
							Fqn = "@my-app/root.ts::Root",
							Accessibility = "Public",
							StartLine = 1,
							EndLine = 1,
							Namespace = null
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
			"root.ts",
			"/project/root.ts",
			"root.ts",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldContain(s => s.Name == "Root");
		symbolBuffer.First(s => s.Name == "Root").Namespace.ShouldBeNullOrEmpty();
	}

	[Theory]
	[InlineData("Protected", Accessibility.Public, 0)]
	[InlineData("Private", Accessibility.Public, 0)]
	[InlineData("Protected", Accessibility.Protected, 1)]
	[InlineData("Public", Accessibility.Public, 1)]
	[InlineData("UnknownAccess", Accessibility.Public, 1)]
	public async Task GivenSymbolAccessibility_WhenMinAccessibilityFilters_ThenIncludesOrExcludesCorrectly(
		string symbolAccessibility, Accessibility minAccessibility, int expectedCount)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		TypeScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<TypeScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/foo.ts", new(""));

		TsAnalysisResult analysisResult = new()
		{
			ProjectName = "my-app",
			ProjectRoot = "/project",
			Files = new()
			{
				["src/foo.ts"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "Foo",
							Kind = "TypeScriptClass",
							Class = "class",
							Fqn = "@my-app/src/foo.ts::Foo",
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
			"src/foo.ts",
			"/project/src/foo.ts",
			"src/foo.ts",
			symbolBuffer,
			relBuffer,
			minAccessibility);

		// Assert
		symbolBuffer.Count.ShouldBe(expectedCount);
	}
}
