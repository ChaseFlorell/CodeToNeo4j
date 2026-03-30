using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Mapping;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Technologies.Web.npm;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Technologies.Web.npm;

public class PackageJsonHandlerTests
{
	private static IConfigurationService CreateConfigService()
	{
		IConfigurationService fake = A.Fake<IConfigurationService>();
		A.CallTo(() => fake.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration(["package.json"], "json"));
		return fake;
	}

	private static PackageJsonHandler CreateSut(MockFileSystem fileSystem)
		=> new(fileSystem, new TextSymbolMapper(), NullLogger<PackageJsonHandler>.Instance, CreateConfigService());

	[Theory]
	[InlineData("package.json")]
	[InlineData("src/package.json")]
	[InlineData("src/app/PACKAGE.JSON")]
	public void GivenPackageJsonPath_WhenCanHandleCalled_ThenReturnsTrue(string filePath)
	{
		var sut = CreateSut(new());
		sut.CanHandle(filePath).ShouldBeTrue();
	}

	[Theory]
	[InlineData("package-lock.json")]
	[InlineData("other.json")]
	[InlineData("tsconfig.json")]
	public void GivenNonPackageJsonPath_WhenCanHandleCalled_ThenReturnsFalse(string filePath)
	{
		var sut = CreateSut(new());
		sut.CanHandle(filePath).ShouldBeFalse();
	}

	[Fact]
	public async Task GivenPackageJsonWithDependencies_WhenHandleCalled_ThenAddsDependencySymbolsAndRelationships()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);
		var content = """
		              {
		                "name": "my-app",
		                "dependencies": {
		                  "lodash": "^4.17.21",
		                  "axios": "^1.6.0"
		                },
		                "devDependencies": {
		                  "typescript": "^5.0.0"
		                }
		              }
		              """;
		var filePath = "package.json";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"package.json",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.Count.ShouldBe(3);

		var lodash = symbolBuffer.FirstOrDefault(s => s.Name == "lodash");
		lodash.ShouldNotBeNull();
		lodash.Key.ShouldBe("pkg:lodash");
		lodash.Kind.ShouldBe("Dependency");
		lodash.Version.ShouldBe("^4.17.21");
		lodash.Documentation.ShouldBe("^4.17.21");

		var axios = symbolBuffer.FirstOrDefault(s => s.Name == "axios");
		axios.ShouldNotBeNull();
		axios.Key.ShouldBe("pkg:axios");
		axios.Kind.ShouldBe("Dependency");

		var typescript = symbolBuffer.FirstOrDefault(s => s.Name == "typescript");
		typescript.ShouldNotBeNull();
		typescript.Key.ShouldBe("pkg:typescript");
		typescript.Kind.ShouldBe("Dependency");

		relBuffer.ShouldContain(r => r.FromKey == "package.json" && r.ToKey == "pkg:lodash" && r.RelType == GraphSchema.Relationships.DependsOn);
		relBuffer.ShouldContain(r => r.FromKey == "package.json" && r.ToKey == "pkg:axios" && r.RelType == GraphSchema.Relationships.DependsOn);
		relBuffer.ShouldContain(r => r.FromKey == "package.json" && r.ToKey == "pkg:typescript" && r.RelType == GraphSchema.Relationships.DependsOn);
	}

	[Fact]
	public async Task GivenPackageJsonWithOnlyDependencies_WhenHandleCalled_ThenIgnoresMissingDevDependencies()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);
		var content = """
		              {
		                "name": "my-lib",
		                "dependencies": {
		                  "react": "^18.0.0"
		                }
		              }
		              """;
		var filePath = "package.json";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"package.json",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.Count.ShouldBe(1);
		symbolBuffer.First().Name.ShouldBe("react");
	}

	[Theory]
	[InlineData("src/app/package.json", "src/app")]
	[InlineData("packages/ui/package.json", "packages/ui")]
	[InlineData("package.json", "")]
	public async Task GivenPackageJsonInSubfolder_WhenHandleCalled_ThenNamespaceIsDirectory(string filePath, string expectedNamespace)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);
		var content = """{"dependencies": {"lodash": "^4.0.0"}}""";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null,
			null,
			"test-repo",
			filePath,
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		result.Namespace.ShouldBe(expectedNamespace);
	}

	[Fact]
	public async Task GivenEmptyPackageJson_WhenHandleCalled_ThenNoSymbols()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);
		var content = """{"name": "empty-app", "version": "1.0.0"}""";
		var filePath = "package.json";
		fileSystem.AddFile(filePath, new(content));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		await sut.Handle(
			null,
			null,
			"test-repo",
			"package.json",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	[Fact]
	public async Task GivenInvalidJson_WhenHandleCalled_ThenNoSymbolsAndNoException()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);
		var filePath = "package.json";
		fileSystem.AddFile(filePath, new("{ invalid json }"));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act - should not throw
		await sut.Handle(
			null,
			null,
			"test-repo",
			"package.json",
			filePath,
			filePath,
			symbolBuffer,
			relBuffer,
			Accessibility.Private);

		// Assert
		symbolBuffer.ShouldBeEmpty();
		relBuffer.ShouldBeEmpty();
	}

	// ── URL enrichment tests ──────────────────────────────────────────────────

	[Fact]
	public async Task GivenNpmLayout_AndInstalledPackageHasBothUrls_WhenHandled_ThenReturnsUrlNodes()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);

		fileSystem.AddFile("package.json", new("""{"dependencies":{"lodash":"^4.17.21"}}"""));
		fileSystem.AddFile("node_modules/lodash/package.json", new("""
		                                                           {
		                                                             "name": "lodash",
		                                                             "homepage": "https://lodash.com",
		                                                             "repository": { "type": "git", "url": "git+https://github.com/lodash/lodash.git" }
		                                                           }
		                                                           """));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "package.json",
			"package.json", "package.json",
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldNotBeNull();
		result.UrlNodes.Count.ShouldBe(2);

		var homepageNode = result.UrlNodes.FirstOrDefault(u => u.Name == "https://lodash.com");
		homepageNode.ShouldNotBeNull();
		homepageNode.DepKey.ShouldBe("pkg:lodash");
		homepageNode.UrlKey.ShouldBe("url:https://lodash.com");

		var repoNode = result.UrlNodes.FirstOrDefault(u => u.Name == "https://github.com/lodash/lodash");
		repoNode.ShouldNotBeNull();
		repoNode.DepKey.ShouldBe("pkg:lodash");
		repoNode.UrlKey.ShouldBe("url:https://github.com/lodash/lodash");
	}

	[Fact]
	public async Task GivenNpmLayout_AndRepositoryIsPlainString_WhenHandled_ThenNormalizesUrl()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);

		fileSystem.AddFile("package.json", new("""{"dependencies":{"react":"^18.0.0"}}"""));
		fileSystem.AddFile("node_modules/react/package.json", new("""
		                                                          {
		                                                            "name": "react",
		                                                            "repository": "github:facebook/react"
		                                                          }
		                                                          """));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "package.json",
			"package.json", "package.json",
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldNotBeNull();
		result.UrlNodes.Count.ShouldBe(1);
		result.UrlNodes.First().Name.ShouldBe("https://github.com/facebook/react");
	}

	[Fact]
	public async Task GivenPnpmLayout_AndInstalledPackageHasUrls_WhenHandled_ThenReturnsUrlNodes()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);

		fileSystem.AddFile("package.json", new("""{"dependencies":{"lodash":"^4.17.21"}}"""));
		// No npm-style path — only pnpm virtual store
		fileSystem.AddFile("node_modules/.pnpm/lodash@4.17.21/node_modules/lodash/package.json", new("""
		                                                                                             {
		                                                                                               "name": "lodash",
		                                                                                               "homepage": "https://lodash.com",
		                                                                                               "repository": { "type": "git", "url": "git+https://github.com/lodash/lodash.git" }
		                                                                                             }
		                                                                                             """));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "package.json",
			"package.json", "package.json",
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldNotBeNull();
		result.UrlNodes.Count.ShouldBe(2);
		result.UrlNodes.ShouldContain(u => u.DepKey == "pkg:lodash" && u.Name == "https://lodash.com");
		result.UrlNodes.ShouldContain(u => u.DepKey == "pkg:lodash" && u.Name == "https://github.com/lodash/lodash");
	}

	[Fact]
	public async Task GivenPnpmLayout_AndScopedPackage_WhenHandled_ThenResolvesCorrectly()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);

		fileSystem.AddFile("package.json", new("""{"devDependencies":{"@types/node":"^20.0.0"}}"""));
		fileSystem.AddFile("node_modules/.pnpm/@types+node@20.0.0/node_modules/@types/node/package.json", new("""
		                                                                                                      {
		                                                                                                        "name": "@types/node",
		                                                                                                        "homepage": "https://github.com/DefinitelyTyped/DefinitelyTyped/tree/master/types/node"
		                                                                                                      }
		                                                                                                      """));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "package.json",
			"package.json", "package.json",
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldNotBeNull();
		result.UrlNodes.Count.ShouldBe(1);
		result.UrlNodes.First().DepKey.ShouldBe("pkg:@types/node");
	}

	[Fact]
	public async Task GivenNoNodeModules_WhenHandled_ThenNoUrlNodesAndNoException()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);

		fileSystem.AddFile("package.json", new("""{"dependencies":{"lodash":"^4.17.21"}}"""));
		// No node_modules at all

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "package.json",
			"package.json", "package.json",
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldBeNull();
		symbolBuffer.Count.ShouldBe(1); // dependency symbol still created
	}

	[Fact]
	public async Task GivenInstalledPackageHasNoUrlFields_WhenHandled_ThenNoUrlNodes()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);

		fileSystem.AddFile("package.json", new("""{"dependencies":{"my-pkg":"1.0.0"}}"""));
		fileSystem.AddFile("node_modules/my-pkg/package.json", new("""{"name":"my-pkg","version":"1.0.0"}"""));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act
		var result = await sut.Handle(
			null, null,
			"test-repo", "package.json",
			"package.json", "package.json",
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldBeNull();
	}

	[Theory]
	[InlineData("""{"dependencies":{"bad-pkg":"1.0.0"}}""", "not valid json {{")]
	[InlineData("""{"dependencies":{"bad-pkg":"1.0.0"}}""", "{ \"broken\": }")]
	public async Task GivenInstalledPackageJsonIsMalformed_WhenHandled_ThenNoUrlNodesAndNoException(string rootContent, string installedContent)
	{
		// Arrange
		MockFileSystem fileSystem = new();
		var sut = CreateSut(fileSystem);

		fileSystem.AddFile("package.json", new(rootContent));
		fileSystem.AddFile("node_modules/bad-pkg/package.json", new(installedContent));

		List<Symbol> symbolBuffer = [];
		List<Relationship> relBuffer = [];

		// Act — should not throw
		var result = await sut.Handle(
			null, null,
			"test-repo", "package.json",
			"package.json", "package.json",
			symbolBuffer, relBuffer,
			Accessibility.Private);

		// Assert
		result.UrlNodes.ShouldBeNull();
		symbolBuffer.Count.ShouldBe(1); // dependency symbol still created
	}

	// ── NormalizeRepositoryUrl tests ──────────────────────────────────────────

	[Theory]
	[InlineData(null, null)]
	[InlineData("", null)]
	// Shorthand notations
	[InlineData("github:user/repo", "https://github.com/user/repo")]
	[InlineData("gitlab:user/repo", "https://gitlab.com/user/repo")]
	[InlineData("bitbucket:user/repo", "https://bitbucket.org/user/repo")]
	// git+ssh variants
	[InlineData("git+ssh://git@github.com/user/repo.git", "https://github.com/user/repo")]
	[InlineData("git+ssh://git@gitlab.com/user/repo.git", "https://gitlab.com/user/repo")]
	[InlineData("git+ssh://git@bitbucket.org/user/repo.git", "https://bitbucket.org/user/repo")]
	[InlineData("git+ssh://git@dev.azure.com/org/project/_git/repo.git", "https://dev.azure.com/org/project/_git/repo")]
	// Plain ssh variants
	[InlineData("ssh://git@github.com/user/repo.git", "https://github.com/user/repo")]
	[InlineData("ssh://git@gitlab.com/user/repo.git", "https://gitlab.com/user/repo")]
	[InlineData("ssh://git@bitbucket.org/user/repo.git", "https://bitbucket.org/user/repo")]
	[InlineData("ssh://git@dev.azure.com/org/project/_git/repo.git", "https://dev.azure.com/org/project/_git/repo")]
	// git+ HTTPS/HTTP wrappers
	[InlineData("git+https://github.com/user/repo.git", "https://github.com/user/repo")]
	[InlineData("git+http://example.com/repo.git", "http://example.com/repo")]
	// Bare git:// protocol
	[InlineData("git://github.com/user/repo.git", "https://github.com/user/repo")]
	// Already HTTPS — no prefix change, just .git stripped
	[InlineData("https://github.com/user/repo.git", "https://github.com/user/repo")]
	// Already clean URL — no change
	[InlineData("https://github.com/user/repo", "https://github.com/user/repo")]
	// Leading/trailing whitespace
	[InlineData("  github:user/repo  ", "https://github.com/user/repo")]
	// Case-insensitive prefix matching
	[InlineData("GITHUB:user/repo", "https://github.com/user/repo")]
	public void GivenRepositoryUrl_WhenNormalizeRepositoryUrlCalled_ThenReturnsExpectedUrl(string? input, string? expected) =>
		PackageJsonHandler.NormalizeRepositoryUrl(input).ShouldBe(expected);

	[Theory]
	[InlineData("https://user:token@github.com/user/repo", "https://github.com/user/repo")]
	[InlineData("https://org@dev.azure.com/org/project/_git/repo", "https://dev.azure.com/org/project/_git/repo")]
	[InlineData("https://user:pass@gitlab.com/user/repo.git", "https://gitlab.com/user/repo")]
	public void GivenUrlWithEmbeddedCredentials_WhenNormalizeRepositoryUrlCalled_ThenStripsCredentials(string input, string expected) =>
		PackageJsonHandler.NormalizeRepositoryUrl(input).ShouldBe(expected);

}
