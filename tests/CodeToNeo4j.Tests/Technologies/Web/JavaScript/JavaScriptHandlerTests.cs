using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.Web.JavaScript;
using CodeToNeo4j.TypeScript.Bridge;
using CodeToNeo4j.TypeScript.Models;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.Web.JavaScript;

public class JavaScriptHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration([".js"], "javascript", "node", KindPrefix: "JavaScript"));
		return fake;
	}

	[Fact]
	public void GivenJavaScriptHandler_WhenFileExtensionAndCanHandleChecked_ThenMatchesJsOnly()
	{
		JavaScriptHandler sut = new(
			new MockFileSystem(),
			new TextSymbolMapper(),
			A.Fake<ITypeScriptBridgeService>(),
			NullLogger<JavaScriptHandler>.Instance,
			CreateConfigService());

		sut.FileExtension.ShouldBe(".js");
		sut.CanHandle("app.js").ShouldBeTrue();
		sut.CanHandle("app.JS").ShouldBeTrue();
		sut.CanHandle("app.ts").ShouldBeFalse();
	}

	[Fact]
	public async Task GivenJsFile_WhenBridgeReturnsResult_ThenExtractsSymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		JavaScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<JavaScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/test.js", new("function foo() {}"));

		TsAnalysisResult analysisResult = new()
		{
			ProjectName = "my-app",
			ProjectRoot = "/project",
			Files = new()
			{
				["src/test.js"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "foo",
							Kind = "JavaScriptFunction",
							Class = "function",
							Fqn = "@my-app/src/test.js::foo",
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
							FromSymbol = "foo",
							FromKind = "function",
							FromLine = 1,
							ToSymbol = "bar",
							ToKind = "function",
							RelType = GraphSchema.Relationships.Invokes
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
			"src/test.js",
			"/project/src/test.js",
			"src/test.js",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldContain(s => s.Name == "foo" && s.Kind == "JavaScriptFunction");
		relBuffer.ShouldContain(r => r.RelType == GraphSchema.Relationships.Invokes);
	}

	[Fact]
	public async Task GivenJsFile_WhenBridgeReturnsNull_ThenReturnsEmptyResult()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		ITypeScriptBridgeService bridgeService = A.Fake<ITypeScriptBridgeService>();
		JavaScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<JavaScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/test.js", new("function foo() {}"));

		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns((TsAnalysisResult?)null);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"src/test.js",
			"/project/src/test.js",
			"src/test.js",
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
		JavaScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<JavaScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/orphan/test.js", new("function foo() {}"));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"orphan/test.js",
			"/orphan/test.js",
			"orphan/test.js",
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
		JavaScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<JavaScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/test.js", new(""));

		TsAnalysisResult analysisResult = new()
		{
			ProjectName = "my-app",
			ProjectRoot = "/project",
			Files = new() { ["src/other.js"] = new() { Symbols = [], Relationships = [] } }
		};
		A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"src/test.js",
			"/project/src/test.js",
			"src/test.js",
			symbolBuffer,
			relBuffer,
			Accessibility.NotApplicable);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
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
		JavaScriptHandler sut = new(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<JavaScriptHandler>.Instance, CreateConfigService());

		fileSystem.AddFile("/project/package.json", new("{}"));
		fileSystem.AddFile("/project/src/test.js", new(""));

		TsAnalysisResult analysisResult = new()
		{
			ProjectName = "my-app",
			ProjectRoot = "/project",
			Files = new()
			{
				["src/test.js"] = new()
				{
					Symbols =
					[
						new()
						{
							Name = "foo",
							Kind = "JavaScriptFunction",
							Class = "function",
							Fqn = "@my-app/src/test.js::foo",
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
			"src/test.js",
			"/project/src/test.js",
			"src/test.js",
			symbolBuffer,
			relBuffer,
			minAccessibility);

		// Assert
		symbolBuffer.Count.ShouldBe(expectedCount);
	}
}
