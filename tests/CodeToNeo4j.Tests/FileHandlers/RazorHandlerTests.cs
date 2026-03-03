using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class RazorHandlerTests
{
    [Fact]
    public async Task GivenRazorWithDirectives_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new RazorHandler(fileSystem);
        var content = @"
@using System.Text
@inject IMyService MyService
@model MyViewModel
@inherits MyBasePage
<h1>Hello</h1>";
        var filePath = "test.razor";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test-file",
            filePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            databaseName: "neo4j",
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.Any(s => s.Kind == "UsingDirective" && s.Name == "System.Text").ShouldBeTrue();
        symbolBuffer.Any(s => s.Kind == "InjectDirective" && s.Name == "IMyService MyService").ShouldBeTrue();
        symbolBuffer.Any(s => s.Kind == "ModelDirective" && s.Name == "MyViewModel").ShouldBeTrue();
        symbolBuffer.Any(s => s.Kind == "InheritsDirective" && s.Name == "MyBasePage").ShouldBeTrue();
        
        relBuffer.Count.ShouldBe(4);
    }
}
