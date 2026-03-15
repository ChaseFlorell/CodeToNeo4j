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
    public async Task GivenJsInSubfolder_WhenHandleCalled_ThenNamespaceIsDirectory()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem);
        var content = "function foo() {}";
        var filePath = "src/utils/test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        var result = await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        result.Namespace.ShouldBe("src/utils");
        symbolBuffer.First().Namespace.ShouldBe("src/utils");
    }

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
            fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
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

        relBuffer.ShouldContain(r => r.FromKey == "test.js" && r.ToKey == importSymbol.Key && r.RelType == "DEPENDS_ON");
        relBuffer.ShouldContain(r => r.FromKey == "test.js" && r.ToKey == functionSymbol.Key && r.RelType == "CONTAINS");
    }

    [Fact]
    public async Task GivenFunctionThatCallsAnotherFunction_WhenHandleCalled_ThenAddsExecutesRelationship()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem);
        var content = @"
function validate(order) {
    return order != null;
}
function processOrder(order) {
    validate(order);
    save(order);
}
function save(order) {}";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var processOrder = symbolBuffer.FirstOrDefault(s => s.Name == "processOrder");
        var validate = symbolBuffer.FirstOrDefault(s => s.Name == "validate");
        var save = symbolBuffer.FirstOrDefault(s => s.Name == "save");

        processOrder.ShouldNotBeNull();
        validate.ShouldNotBeNull();
        save.ShouldNotBeNull();

        relBuffer.ShouldContain(r => r.FromKey == processOrder!.Key && r.ToKey == validate!.Key && r.RelType == "EXECUTES");
        relBuffer.ShouldContain(r => r.FromKey == processOrder!.Key && r.ToKey == save!.Key && r.RelType == "EXECUTES");
    }

    [Fact]
    public async Task GivenFunctionWithNoCalls_WhenHandleCalled_ThenNoExecutesRelationship()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem);
        var content = "function add(a, b) { return a + b; }";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        relBuffer.ShouldNotContain(r => r.RelType == "EXECUTES");
    }

    [Fact]
    public async Task GivenFunctionThatCallsExternalFunction_WhenHandleCalled_ThenNoExecutesRelationship()
    {
        // Arrange — externalFn is not defined in this file
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem);
        var content = @"
function doWork() {
    externalFn();
}";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        relBuffer.ShouldNotContain(r => r.RelType == "EXECUTES");
    }
}
