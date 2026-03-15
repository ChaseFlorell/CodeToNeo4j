using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class PackageJsonHandlerTests
{
    private static PackageJsonHandler CreateSut(MockFileSystem fileSystem)
        => new(fileSystem, new TextSymbolMapper(), NullLogger<PackageJsonHandler>.Instance);

    [Theory]
    [InlineData("package.json")]
    [InlineData("src/package.json")]
    [InlineData("src/app/PACKAGE.JSON")]
    public void GivenPackageJsonPath_WhenCanHandleCalled_ThenReturnsTrue(string filePath)
    {
        var sut = CreateSut(new MockFileSystem());
        sut.CanHandle(filePath).ShouldBeTrue();
    }

    [Theory]
    [InlineData("package-lock.json")]
    [InlineData("other.json")]
    [InlineData("tsconfig.json")]
    public void GivenNonPackageJsonPath_WhenCanHandleCalled_ThenReturnsFalse(string filePath)
    {
        var sut = CreateSut(new MockFileSystem());
        sut.CanHandle(filePath).ShouldBeFalse();
    }

    [Fact]
    public async Task GivenPackageJsonWithDependencies_WhenHandleCalled_ThenAddsDependencySymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
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
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "package.json",
            filePath: filePath,
            relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

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

        relBuffer.ShouldContain(r => r.FromKey == "package.json" && r.ToKey == "pkg:lodash" && r.RelType == "DEPENDS_ON");
        relBuffer.ShouldContain(r => r.FromKey == "package.json" && r.ToKey == "pkg:axios" && r.RelType == "DEPENDS_ON");
        relBuffer.ShouldContain(r => r.FromKey == "package.json" && r.ToKey == "pkg:typescript" && r.RelType == "DEPENDS_ON");
    }

    [Fact]
    public async Task GivenPackageJsonWithOnlyDependencies_WhenHandleCalled_ThenIgnoresMissingDevDependencies()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
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
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "package.json",
            filePath: filePath,
            relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.Count.ShouldBe(1);
        symbolBuffer.First().Name.ShouldBe("react");
    }

    [Fact]
    public async Task GivenPackageJsonInSubfolder_WhenHandleCalled_ThenNamespaceIsDirectory()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);
        var content = """{"dependencies": {"lodash": "^4.0.0"}}""";
        var filePath = "src/app/package.json";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "src/app/package.json",
            filePath: filePath,
            relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        result.Namespace.ShouldBe("src/app");
    }

    [Fact]
    public async Task GivenEmptyPackageJson_WhenHandleCalled_ThenNoSymbols()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);
        var content = """{"name": "empty-app", "version": "1.0.0"}""";
        var filePath = "package.json";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "package.json",
            filePath: filePath,
            relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldBeEmpty();
        relBuffer.ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenInvalidJson_WhenHandleCalled_ThenNoSymbolsAndNoException()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);
        var filePath = "package.json";
        fileSystem.AddFile(filePath, new MockFileData("{ invalid json }"));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act - should not throw
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "package.json",
            filePath: filePath,
            relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldBeEmpty();
        relBuffer.ShouldBeEmpty();
    }

    // ── URL enrichment tests ──────────────────────────────────────────────────

    [Fact]
    public async Task GivenNpmLayout_AndInstalledPackageHasBothUrls_WhenHandled_ThenReturnsUrlNodes()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        fileSystem.AddFile("package.json", new MockFileData("""{"dependencies":{"lodash":"^4.17.21"}}"""));
        fileSystem.AddFile("node_modules/lodash/package.json", new MockFileData("""
            {
              "name": "lodash",
              "homepage": "https://lodash.com",
              "repository": { "type": "git", "url": "git+https://github.com/lodash/lodash.git" }
            }
            """));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "package.json",
            filePath: "package.json", relativePath: "package.json",
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

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
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        fileSystem.AddFile("package.json", new MockFileData("""{"dependencies":{"react":"^18.0.0"}}"""));
        fileSystem.AddFile("node_modules/react/package.json", new MockFileData("""
            {
              "name": "react",
              "repository": "github:facebook/react"
            }
            """));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "package.json",
            filePath: "package.json", relativePath: "package.json",
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        result.UrlNodes.ShouldNotBeNull();
        result.UrlNodes.Count.ShouldBe(1);
        result.UrlNodes.First().Name.ShouldBe("https://github.com/facebook/react");
    }

    [Fact]
    public async Task GivenPnpmLayout_AndInstalledPackageHasUrls_WhenHandled_ThenReturnsUrlNodes()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        fileSystem.AddFile("package.json", new MockFileData("""{"dependencies":{"lodash":"^4.17.21"}}"""));
        // No npm-style path — only pnpm virtual store
        fileSystem.AddFile("node_modules/.pnpm/lodash@4.17.21/node_modules/lodash/package.json", new MockFileData("""
            {
              "name": "lodash",
              "homepage": "https://lodash.com",
              "repository": { "type": "git", "url": "git+https://github.com/lodash/lodash.git" }
            }
            """));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "package.json",
            filePath: "package.json", relativePath: "package.json",
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

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
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        fileSystem.AddFile("package.json", new MockFileData("""{"devDependencies":{"@types/node":"^20.0.0"}}"""));
        fileSystem.AddFile("node_modules/.pnpm/@types+node@20.0.0/node_modules/@types/node/package.json", new MockFileData("""
            {
              "name": "@types/node",
              "homepage": "https://github.com/DefinitelyTyped/DefinitelyTyped/tree/master/types/node"
            }
            """));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "package.json",
            filePath: "package.json", relativePath: "package.json",
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        result.UrlNodes.ShouldNotBeNull();
        result.UrlNodes.Count.ShouldBe(1);
        result.UrlNodes.First().DepKey.ShouldBe("pkg:@types/node");
    }

    [Fact]
    public async Task GivenNoNodeModules_WhenHandled_ThenNoUrlNodesAndNoException()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        fileSystem.AddFile("package.json", new MockFileData("""{"dependencies":{"lodash":"^4.17.21"}}"""));
        // No node_modules at all

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "package.json",
            filePath: "package.json", relativePath: "package.json",
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        result.UrlNodes.ShouldBeNull();
        symbolBuffer.Count.ShouldBe(1); // dependency symbol still created
    }

    [Fact]
    public async Task GivenInstalledPackageHasNoUrlFields_WhenHandled_ThenNoUrlNodes()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        fileSystem.AddFile("package.json", new MockFileData("""{"dependencies":{"my-pkg":"1.0.0"}}"""));
        fileSystem.AddFile("node_modules/my-pkg/package.json", new MockFileData("""{"name":"my-pkg","version":"1.0.0"}"""));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "package.json",
            filePath: "package.json", relativePath: "package.json",
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        result.UrlNodes.ShouldBeNull();
    }

    [Fact]
    public async Task GivenInstalledPackageJsonIsMalformed_WhenHandled_ThenNoUrlNodesAndNoException()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = CreateSut(fileSystem);

        fileSystem.AddFile("package.json", new MockFileData("""{"dependencies":{"bad-pkg":"1.0.0"}}"""));
        fileSystem.AddFile("node_modules/bad-pkg/package.json", new MockFileData("not valid json {{"));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act — should not throw
        var result = await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "package.json",
            filePath: "package.json", relativePath: "package.json",
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        result.UrlNodes.ShouldBeNull();
        symbolBuffer.Count.ShouldBe(1); // dependency symbol still created
    }
}
