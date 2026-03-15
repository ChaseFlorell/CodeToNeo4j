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

        // Check for PackageReference
        var packageSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "Newtonsoft.Json");
        packageSymbol.ShouldNotBeNull();
        packageSymbol.Kind.ShouldBe("PackageReference");
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
}
