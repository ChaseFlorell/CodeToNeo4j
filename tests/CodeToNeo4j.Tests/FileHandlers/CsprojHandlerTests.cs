using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CsprojHandlerTests
{
    [Fact]
    public async Task GivenCsprojWithPackageAndProjectReferences_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CsprojHandler(fileSystem, new TextSymbolMapper());

        var content = @"
<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
    <ProjectReference Include=""..\MyLib\MyLib.csproj"" />
  </ItemGroup>
</Project>";
        var filePath = "test.csproj";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        // Check for Property
        var propertySymbol = symbolBuffer.FirstOrDefault(s => s.Name == "TargetFramework");
        propertySymbol.ShouldNotBeNull();
        propertySymbol.Kind.ShouldBe("ProjectProperty");
        propertySymbol.Documentation.ShouldBe("net8.0");

        // Check for Dependency (package reference unified as Dependency node)
        var packageSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Newtonsoft.Json");
        packageSymbol.ShouldNotBeNull();
        packageSymbol.Key.ShouldBe("pkg:Newtonsoft.Json");
        packageSymbol.Kind.ShouldBe("Dependency");
        packageSymbol.Documentation.ShouldBe("13.0.1");
        packageSymbol.Version.ShouldBe("13.0.1");

        // Check for ProjectReference
        var projectSymbol = symbolBuffer.FirstOrDefault(s => s.Name == @"..\MyLib\MyLib.csproj");
        projectSymbol.ShouldNotBeNull();
        projectSymbol.Kind.ShouldBe("ProjectReference");

        // Check relationships
        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == propertySymbol.Key && r.RelType == "HAS_PROPERTY");
        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == packageSymbol.Key && r.RelType == "DEPENDS_ON");
        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == projectSymbol.Key && r.RelType == "DEPENDS_ON");
    }

    [Fact]
    public async Task GivenPackageReference_AndNuspecHasBothUrls_WhenHandled_ThenCreatesUrlSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CsprojHandler(fileSystem, new TextSymbolMapper());

        const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.1"" />
  </ItemGroup>
</Project>";
        const string filePath = "test.csproj";
        fileSystem.AddFile(filePath, new MockFileData(content));

        const string nuspecContent = """
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <projectUrl>https://www.newtonsoft.com/json</projectUrl>
                <repository type="git" url="https://github.com/JamesNK/Newtonsoft.Json" />
              </metadata>
            </package>
            """;
        fileSystem.AddFile(NuspecPath("Newtonsoft.Json", "13.0.1"), new MockFileData(nuspecContent));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var projectUrlSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "Url" && s.Name == "https://www.newtonsoft.com/json");
        projectUrlSymbol.ShouldNotBeNull();
        projectUrlSymbol.Key.ShouldBe("url:https://www.newtonsoft.com/json");
        projectUrlSymbol.Class.ShouldBe("Url");

        var repoUrlSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "Url" && s.Name == "https://github.com/JamesNK/Newtonsoft.Json");
        repoUrlSymbol.ShouldNotBeNull();
        repoUrlSymbol.Key.ShouldBe("url:https://github.com/JamesNK/Newtonsoft.Json");

        relBuffer.ShouldContain(r =>
            r.FromKey == "pkg:Newtonsoft.Json" &&
            r.ToKey == "url:https://www.newtonsoft.com/json" &&
            r.RelType == "HAS_PROJECT_URL");
        relBuffer.ShouldContain(r =>
            r.FromKey == "pkg:Newtonsoft.Json" &&
            r.ToKey == "url:https://github.com/JamesNK/Newtonsoft.Json" &&
            r.RelType == "HAS_REPOSITORY_URL");
    }

    [Fact]
    public async Task GivenPackageReference_AndNuspecHasOnlyProjectUrl_WhenHandled_ThenOnlyHasProjectUrlRelationship()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CsprojHandler(fileSystem, new TextSymbolMapper());

        const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Serilog"" Version=""3.0.0"" />
  </ItemGroup>
</Project>";
        const string filePath = "test.csproj";
        fileSystem.AddFile(filePath, new MockFileData(content));

        const string nuspecContent = """
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <projectUrl>https://serilog.net</projectUrl>
              </metadata>
            </package>
            """;
        fileSystem.AddFile(NuspecPath("Serilog", "3.0.0"), new MockFileData(nuspecContent));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.Count(s => s.Kind == "Url").ShouldBe(1);
        relBuffer.ShouldContain(r => r.RelType == "HAS_PROJECT_URL");
        relBuffer.ShouldNotContain(r => r.RelType == "HAS_REPOSITORY_URL");
    }

    [Fact]
    public async Task GivenPackageReference_AndNuspecHasOnlyRepositoryUrl_WhenHandled_ThenOnlyHasRepositoryUrlRelationship()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CsprojHandler(fileSystem, new TextSymbolMapper());

        const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""MyLib"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        const string filePath = "test.csproj";
        fileSystem.AddFile(filePath, new MockFileData(content));

        const string nuspecContent = """
            <?xml version="1.0"?>
            <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
              <metadata>
                <repository type="git" url="https://github.com/org/mylib" />
              </metadata>
            </package>
            """;
        fileSystem.AddFile(NuspecPath("MyLib", "1.0.0"), new MockFileData(nuspecContent));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.Count(s => s.Kind == "Url").ShouldBe(1);
        relBuffer.ShouldNotContain(r => r.RelType == "HAS_PROJECT_URL");
        relBuffer.ShouldContain(r => r.RelType == "HAS_REPOSITORY_URL");
    }

    [Fact]
    public async Task GivenPackageReference_AndNuspecIsMissing_WhenHandled_ThenNoUrlSymbols()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CsprojHandler(fileSystem, new TextSymbolMapper());

        const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""SomePackage"" Version=""2.0.0"" />
  </ItemGroup>
</Project>";
        const string filePath = "test.csproj";
        fileSystem.AddFile(filePath, new MockFileData(content));
        // No nuspec added to MockFileSystem

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldNotContain(s => s.Kind == "Url");
        relBuffer.ShouldNotContain(r => r.RelType == "HAS_PROJECT_URL");
        relBuffer.ShouldNotContain(r => r.RelType == "HAS_REPOSITORY_URL");
    }

    [Fact]
    public async Task GivenPackageReference_AndNuspecIsMalformed_WhenHandled_ThenNoUrlSymbolsAndNoException()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CsprojHandler(fileSystem, new TextSymbolMapper());

        const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""BrokenPkg"" Version=""1.0.0"" />
  </ItemGroup>
</Project>";
        const string filePath = "test.csproj";
        fileSystem.AddFile(filePath, new MockFileData(content));
        fileSystem.AddFile(NuspecPath("BrokenPkg", "1.0.0"), new MockFileData("this is not valid xml <<>>"));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act - should not throw
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldNotContain(s => s.Kind == "Url");
        relBuffer.ShouldNotContain(r => r.RelType == "HAS_PROJECT_URL");
        relBuffer.ShouldNotContain(r => r.RelType == "HAS_REPOSITORY_URL");
    }

    [Fact]
    public async Task GivenPackageReferenceWithNoVersion_WhenHandled_ThenNoUrlSymbols()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CsprojHandler(fileSystem, new TextSymbolMapper());

        const string content = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""SomePackage"" />
  </ItemGroup>
</Project>";
        const string filePath = "test.csproj";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test-file",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldNotContain(s => s.Kind == "Url");
    }

    private static string NuspecPath(string name, string version)
    {
        var packagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
        var nameNormalized = name.ToLowerInvariant();
        return Path.Combine(packagesRoot, nameNormalized, version, $"{nameNormalized}.nuspec");
    }
}
