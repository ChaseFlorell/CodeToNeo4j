using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class JsonHandlerTests
{
    [Fact]
    public async Task GivenJsonWithNestedProperties_WhenHandleCalled_ThenAddsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var logger = A.Fake<ILogger<JsonHandler>>();
        var sut = new JsonHandler(fileSystem, logger);
        const string content = @"{ ""foo"": { ""bar"": 123 }, ""baz"": [1, 2] }";
        const string filePath = "test.json";
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
        var fooSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "foo");
        fooSymbol.ShouldNotBeNull();
        fooSymbol.Fqn.ShouldBe("foo");
        fooSymbol.Class.ShouldBe("property");

        var barSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "bar");
        barSymbol.ShouldNotBeNull();
        barSymbol.Fqn.ShouldBe("foo.bar");
        barSymbol.Class.ShouldBe("property");

        var bazSymbol = symbolBuffer.FirstOrDefault(s => s.Name == "baz");
        bazSymbol.ShouldNotBeNull();
        bazSymbol.Fqn.ShouldBe("baz");
        bazSymbol.Class.ShouldBe("property");

        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == fooSymbol.Key && r.RelType == "CONTAINS");
        relBuffer.ShouldContain(r => r.FromKey == "test-file" && r.ToKey == bazSymbol.Key && r.RelType == "CONTAINS");
    }
}