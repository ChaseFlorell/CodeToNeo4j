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
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
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
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
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
    public async Task GivenFunctionThatCallsAnotherFunction_WhenHandleCalled_ThenAddsInvokesRelationship()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
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

        relBuffer.ShouldContain(r => r.FromKey == processOrder!.Key && r.ToKey == validate!.Key && r.RelType == "INVOKES");
        relBuffer.ShouldContain(r => r.FromKey == processOrder!.Key && r.ToKey == save!.Key && r.RelType == "INVOKES");
    }

    [Fact]
    public async Task GivenFunctionWithNoCalls_WhenHandleCalled_ThenNoInvokesRelationship()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
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
        relBuffer.ShouldNotContain(r => r.RelType == "INVOKES");
    }

    [Fact]
    public async Task GivenDuplicateFunctionNames_WhenHandleCalled_ThenDoesNotThrow()
    {
        // Arrange — two functions with the same name (e.g. redefinitions common in JS)
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = @"
function to(value) { return value; }
function to(value, unit) { return value + unit; }
function caller() { to(1); }";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act — should not throw ArgumentException
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
        symbolBuffer.Where(s => s.Name == "to").ShouldNotBeEmpty();
        relBuffer.ShouldContain(r => r.RelType == "INVOKES");
    }

    [Fact]
    public async Task GivenFunctionThatCallsExternalFunction_WhenHandleCalled_ThenNoInvokesRelationship()
    {
        // Arrange — externalFn is not defined in this file
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
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
        relBuffer.ShouldNotContain(r => r.RelType == "INVOKES");
    }

    [Fact]
    public async Task GivenFunctionWithStringLiteralsContainingBraces_WhenHandleCalled_ThenExtractsFunction()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = """
            function render(name) {
                return `Hello ${name}, welcome to {world}`;
            }
            """;
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldContain(s => s.Name == "render" && s.Kind == "JavaScriptFunction");
    }

    [Fact]
    public async Task GivenArrowFunctionWithDestructuring_WhenHandleCalled_ThenExtractsFunction()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = """
            const processUser = ({ name, age }) => {
                return `${name} is ${age}`;
            };
            """;
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldContain(s => s.Name == "processUser" && s.Kind == "JavaScriptFunction");
    }

    [Fact]
    public async Task GivenReExport_WhenHandleCalled_ThenExtractsImportRelationship()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = """
            import { foo } from './foo';
            import { bar } from './bar';
            """;
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.Count(s => s.Kind == "JavaScriptImport").ShouldBe(2);
        relBuffer.Count(r => r.RelType == "DEPENDS_ON").ShouldBe(2);
    }

    [Fact]
    public async Task GivenRequireCall_WhenHandleCalled_ThenExtractsModuleReference()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = """
            const path = require('./utils/path');
            function main() {}
            """;
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldContain(s => s.Name == "./utils/path" && s.Kind == "JavaScriptImport");
    }

    [Theory]
    [InlineData("lodash")]
    [InlineData("express")]
    [InlineData("@angular/core")]
    public async Task GivenBareModuleSpecifier_WhenHandleCalled_ThenAddsDependsOnWithPkgPrefix(string moduleName)
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = $"import something from '{moduleName}';";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        relBuffer.ShouldContain(r => r.FromKey == "test.js" && r.ToKey == $"pkg:{moduleName}" && r.RelType == "DEPENDS_ON");
        symbolBuffer.ShouldNotContain(s => s.Kind == "JavaScriptImport");
    }

    [Fact]
    public async Task GivenFunctionInObjectLiteral_WhenHandleCalled_ThenExtractsFunction()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = "const obj = { foo: function() {} };";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldContain(s => s.Name == "foo" && s.Kind == "JavaScriptFunction");
    }

    [Fact]
    public async Task GivenAbsoluteModuleSpecifier_WhenHandleCalled_ThenDoesNotUsePkgPrefix()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = "import something from '/abs/path';";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        relBuffer.ShouldContain(r => r.ToKey.Contains("Import") && r.RelType == "DEPENDS_ON");
        relBuffer.ShouldNotContain(r => r.ToKey == "pkg:/abs/path");
    }

    [Fact]
    public async Task GivenFunctionWithUnclosedBrace_WhenHandleCalled_ThenDoesNotExtractInvokes()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = "function main() { someCall(); "; // Missing closing brace
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        symbolBuffer.ShouldContain(s => s.Name == "main");
        relBuffer.ShouldNotContain(r => r.RelType == "INVOKES");
    }

    [Fact]
    public async Task GivenFunctionWithKeywordsInBody_WhenHandleCalled_ThenSkipsKeywordsInInvokes()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = "function main() { if(true) { return; } while(false) {} someCall(); } function someCall() {}";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.Private);

        // Assert
        var main = symbolBuffer.First(s => s.Name == "main");
        var someCall = symbolBuffer.First(s => s.Name == "someCall");
        relBuffer.ShouldContain(r => r.FromKey == main.Key && r.ToKey == someCall.Key && r.RelType == "INVOKES");
        relBuffer.ShouldNotContain(r => r.ToKey.Contains("if") || r.ToKey.Contains("while"));
    }

    [Fact]
    public async Task GivenMinAccessibilityNotApplicable_WhenHandleCalled_ThenDoesNotAddSymbols()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new JavaScriptHandler(fileSystem, new TextSymbolMapper());
        var content = "function foo() {}";
        var filePath = "test.js";
        fileSystem.AddFile(filePath, new MockFileData(content));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null, compilation: null,
            repoKey: "test-repo", fileKey: "test.js",
            filePath: filePath, relativePath: filePath,
            symbolBuffer: symbolBuffer, relBuffer: relBuffer,
            minAccessibility: Accessibility.NotApplicable);

        // Assert
        symbolBuffer.ShouldBeEmpty();
    }

    [Fact]
    public void GivenJavaScriptHandler_WhenFileExtensionAndCanHandleChecked_ThenMatchesJsOnly()
    {
        var sut = new JavaScriptHandler(new MockFileSystem(), new TextSymbolMapper());
        sut.FileExtension.ShouldBe(".js");
        sut.CanHandle("app.js").ShouldBeTrue();
        sut.CanHandle("app.JS").ShouldBeTrue();
        sut.CanHandle("app.ts").ShouldBeFalse();
    }
}
