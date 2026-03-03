using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class JavaScriptHandlerTests
{
    [Fact]
    public async Task GivenJsWithFunctionAndImport_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem);
        var content = @"
import { foo } from './foo.js';
function myFunction() {
    return foo();
}
const myArrow = () => {};";
        var filePath = "test.js";
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
        var importSymbol = symbolBuffer.FirstOrDefault(s => s.Kind == "JavaScriptImport");
        importSymbol.ShouldNotBeNull();
        importSymbol.Name.ShouldBe("./foo.js");

        var functionSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "myFunction");
        functionSymbol.ShouldNotBeNull();
        functionSymbol.Kind.ShouldBe("JavaScriptFunction");

        var arrowSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "myArrow");
        arrowSymbol.ShouldNotBeNull();
        arrowSymbol.Kind.ShouldBe("JavaScriptFunction");

        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == importSymbol.Key && r.RelType == "DEPENDS_ON");
        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == functionSymbol.Key && r.RelType == "CONTAINS");
    }
}
