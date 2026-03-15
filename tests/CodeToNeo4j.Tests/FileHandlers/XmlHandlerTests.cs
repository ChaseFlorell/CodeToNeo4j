using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class XmlHandlerTests
{
    [Fact]
    public async Task GivenXmlWithElements_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new XmlHandler(fileSystem, new TextSymbolMapper());
        var content = @"<root><child>value</child></root>";
        var filePath = "test.xml";
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
        var rootSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "root");
        rootSymbol.ShouldNotBeNull();
        rootSymbol.Kind.ShouldBe("XmlElement");

        var childSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "child");
        childSymbol.ShouldNotBeNull();
        childSymbol.Kind.ShouldBe("XmlElement");

        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == rootSymbol.Key && r.RelType == "CONTAINS");
        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == childSymbol.Key && r.RelType == "CONTAINS");
    }
}
