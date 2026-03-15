using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class HtmlHandlerTests
{
    [Fact]
    public async Task GivenHtmlWithScriptAndId_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new HtmlHandler(fileSystem, new TextSymbolMapper());
        var content = @"
<html>
  <head>
    <script src=""app.js""></script>
  </head>
  <body>
    <div id=""myId""></div>
  </body>
</html>";
        var filePath = "test.html";
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
        var scriptSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "HtmlScriptReference");
        scriptSymbol.ShouldNotBeNull();
        scriptSymbol.Name.ShouldBe("app.js");

        var idSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "HtmlElementId");
        idSymbol.ShouldNotBeNull();
        idSymbol.Name.ShouldBe("myId");

        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == scriptSymbol.Key && r.RelType == "DEPENDS_ON");
        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == idSymbol.Key && r.RelType == "CONTAINS");
    }
}
