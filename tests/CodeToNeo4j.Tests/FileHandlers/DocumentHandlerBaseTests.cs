using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class DocumentHandlerBaseTests
{
    [Theory]
    [InlineData("hello\nworld", 0, 1)]
    [InlineData("hello\nworld", 6, 2)]
    [InlineData("line1\nline2\nline3", 12, 3)]
    public void GivenContent_WhenGetLineNumberCalled_ThenReturnsCorrectLine(string content, int index, int expected)
    {
        TestHandler.TestGetLineNumber(content, index).ShouldBe(expected);
    }

    [Theory]
    [InlineData(Accessibility.Private, true)]
    [InlineData(Accessibility.Internal, true)]
    [InlineData(Accessibility.Public, true)]
    [InlineData(Accessibility.NotApplicable, false)]
    public void GivenAccessibility_WhenIsPublicAccessibleCalled_ThenReturnsExpected(Accessibility minAccessibility, bool expected)
    {
        TestHandler.TestIsPublicAccessible(minAccessibility).ShouldBe(expected);
    }

    [Fact]
    public async Task GivenDocument_WhenGetContentCalled_ThenReturnsDocumentText()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new TestHandler(fileSystem);
        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "test.cs", SourceText.From("doc content"));

        // Act
        var content = await sut.PublicGetContent(document, "any.cs");

        // Assert
        content.ShouldBe("doc content");
    }

    [Fact]
    public async Task GivenNoDocument_WhenGetContentCalled_ThenReadsFromFileSystem()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddFile("file.cs", new MockFileData("file content"));
        var sut = new TestHandler(fileSystem);

        // Act
        var content = await sut.PublicGetContent(null, "file.cs");

        // Assert
        content.ShouldBe("file content");
    }

    [Fact]
    public async Task GivenHandlerCalledMultipleTimes_WhenNumberOfFilesHandledRead_ThenReflectsCallCount()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var sut = new TestHandler(fileSystem);

        // Act
        await sut.Handle(null, null, null, "a", "a", "a", [], [], Accessibility.NotApplicable);
        await sut.Handle(null, null, null, "b", "b", "b", [], [], Accessibility.NotApplicable);

        // Assert
        sut.NumberOfFilesHandled.ShouldBe(2);
    }

    [Theory]
    [InlineData("file.test", true)]
    [InlineData("FILE.TEST", true)]
    [InlineData("file.cs", false)]
    [InlineData("file.test.bak", false)]
    public void GivenFilePath_WhenCanHandleCalled_ThenReturnsExpected(string filePath, bool expected)
    {
        var sut = new TestHandler(new MockFileSystem());
        sut.CanHandle(filePath).ShouldBe(expected);
    }

    private sealed class TestHandler(MockFileSystem fs) : DocumentHandlerBase(fs)
    {
        public override string FileExtension => ".test";

        protected override Task<FileResult> HandleFile(
            TextDocument? document,
            Compilation? compilation,
            string? repoKey,
            string fileKey,
            string filePath,
            string relativePath,
            ICollection<Symbol> symbolBuffer,
            ICollection<Relationship> relBuffer,
            Accessibility minAccessibility)
            => Task.FromResult(new FileResult(null, fileKey));

        public static int TestGetLineNumber(string content, int index) => GetLineNumber(content, index);
        public static bool TestIsPublicAccessible(Accessibility minAccessibility) => IsPublicAccessible(minAccessibility);
        public Task<string> PublicGetContent(TextDocument? document, string filePath) => GetContent(document, filePath);
    }
}
