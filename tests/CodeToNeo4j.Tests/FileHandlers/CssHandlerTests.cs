using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class CssHandlerTests
{
    [Fact]
    public async Task GivenCssWithSelectors_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CssHandler(fileSystem, new TextSymbolMapper());
        var content = @"body { color: black; }";
        var filePath = "test.css";
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
        var symbol = symbolBuffer.FirstOrDefault(s => s.Name == "body");
        symbol.ShouldNotBeNull();
        symbol.Kind.ShouldBe("CssSelector");

        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == symbol.Key && r.RelType == "CONTAINS");
    }

    [Fact]
    public async Task GivenCssWithAtRules_WhenHandleCalled_ThenSkipsAtRules()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CssHandler(fileSystem, new TextSymbolMapper());
        var content = @"@import ""foo.css""; @media screen { .foo { color: red; } }";
        var filePath = "test.css";
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
        symbolBuffer.ShouldNotContain(s => s.Name.StartsWith("@"));
        symbolBuffer.ShouldContain(s => s.Name == ".foo");
    }

    [Fact]
    public async Task GivenMinAccessibilityNotApplicable_WhenHandleCalled_ThenDoesNotAddSymbols()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new CssHandler(fileSystem, new TextSymbolMapper());
        var content = @".foo { color: red; }";
        var filePath = "test.css";
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
            minAccessibility: Accessibility.NotApplicable);

        // Assert
        symbolBuffer.ShouldBeEmpty();
        relBuffer.ShouldBeEmpty();
    }
}
