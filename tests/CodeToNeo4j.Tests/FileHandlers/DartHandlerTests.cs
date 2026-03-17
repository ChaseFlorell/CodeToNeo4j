using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Dart.Bridge;
using CodeToNeo4j.Dart.Models;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class DartHandlerTests
{
    [Fact]
    public async Task GivenDartFile_WhenBridgeReturnsResult_ThenExtractsSymbolsAndRelationships()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var bridgeService = A.Fake<IDartBridgeService>();
        var sut = new DartHandler(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance);

        var projectRoot = "/project";
        fileSystem.AddFile("/project/pubspec.yaml", new MockFileData("name: test_app"));
        fileSystem.AddFile("/project/lib/src/foo.dart", new MockFileData("class Foo {}"));

        var analysisResult = new DartAnalysisResult
        {
            ProjectName = "test_app",
            ProjectRoot = projectRoot,
            Files = new Dictionary<string, DartFileResult>
            {
                ["lib/src/foo.dart"] = new()
                {
                    Symbols =
                    [
                        new DartSymbolInfo
                        {
                            Name = "Foo",
                            Kind = "DartClass",
                            Class = "class",
                            Fqn = "package:test_app/src/foo.dart::Foo",
                            Accessibility = "Public",
                            StartLine = 1,
                            EndLine = 1,
                            Namespace = "package:test_app/lib/src"
                        }
                    ],
                    Relationships =
                    [
                        new DartRelationshipInfo
                        {
                            FromSymbol = "Foo",
                            FromKind = "class",
                            FromLine = 1,
                            ToSymbol = "Bar",
                            ToKind = "class",
                            RelType = "DEPENDS_ON"
                        }
                    ]
                }
            }
        };

        A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "lib/src/foo.dart",
            filePath: "/project/lib/src/foo.dart",
            relativePath: "lib/src/foo.dart",
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.NotApplicable);

        // Assert
        symbolBuffer.ShouldContain(s => s.Name == "Foo" && s.Kind == "DartClass");
        relBuffer.ShouldContain(r => r.RelType == "DEPENDS_ON");
    }

    [Fact]
    public async Task GivenDartFile_WhenBridgeReturnsNull_ThenReturnsEmptyResult()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var bridgeService = A.Fake<IDartBridgeService>();
        var sut = new DartHandler(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance);

        fileSystem.AddFile("/project/pubspec.yaml", new MockFileData("name: test_app"));
        fileSystem.AddFile("/project/lib/main.dart", new MockFileData("void main() {}"));

        A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns((DartAnalysisResult?)null);

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "lib/main.dart",
            filePath: "/project/lib/main.dart",
            relativePath: "lib/main.dart",
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.NotApplicable);

        // Assert
        symbolBuffer.ShouldBeEmpty();
        relBuffer.ShouldBeEmpty();
    }

    [Fact]
    public async Task GivenDartFile_WhenNoPubspecFound_ThenReturnsEmptyResult()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var bridgeService = A.Fake<IDartBridgeService>();
        var sut = new DartHandler(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance);

        fileSystem.AddFile("/orphan/main.dart", new MockFileData("void main() {}"));

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "orphan/main.dart",
            filePath: "/orphan/main.dart",
            relativePath: "orphan/main.dart",
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: Accessibility.NotApplicable);

        // Assert
        symbolBuffer.ShouldBeEmpty();
        A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).MustNotHaveHappened();
    }

    [Theory]
    [InlineData(Accessibility.Public, 1)]
    [InlineData(Accessibility.Private, 1)]
    [InlineData(Accessibility.NotApplicable, 1)]
    public async Task GivenDartFile_WhenMinAccessibilitySet_ThenFiltersCorrectly(Accessibility minAccessibility, int expectedSymbolCount)
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var bridgeService = A.Fake<IDartBridgeService>();
        var sut = new DartHandler(fileSystem, new TextSymbolMapper(), bridgeService, NullLogger<DartHandler>.Instance);

        fileSystem.AddFile("/project/pubspec.yaml", new MockFileData("name: test_app"));
        fileSystem.AddFile("/project/lib/foo.dart", new MockFileData("class Foo {}"));

        var analysisResult = new DartAnalysisResult
        {
            ProjectName = "test_app",
            ProjectRoot = "/project",
            Files = new Dictionary<string, DartFileResult>
            {
                ["lib/foo.dart"] = new()
                {
                    Symbols =
                    [
                        new DartSymbolInfo
                        {
                            Name = "Foo",
                            Kind = "DartClass",
                            Class = "class",
                            Fqn = "package:test_app/foo.dart::Foo",
                            Accessibility = "Public",
                            StartLine = 1,
                            EndLine = 1,
                        }
                    ],
                    Relationships = []
                }
            }
        };

        A.CallTo(() => bridgeService.AnalyzeProject(A<string>._)).Returns(analysisResult);

        var symbolBuffer = new List<Symbol>();
        var relBuffer = new List<Relationship>();

        // Act
        await sut.Handle(
            document: null,
            compilation: null,
            repoKey: "test-repo",
            fileKey: "lib/foo.dart",
            filePath: "/project/lib/foo.dart",
            relativePath: "lib/foo.dart",
            symbolBuffer: symbolBuffer,
            relBuffer: relBuffer,
            minAccessibility: minAccessibility);

        // Assert
        symbolBuffer.Count.ShouldBe(expectedSymbolCount);
    }
}
