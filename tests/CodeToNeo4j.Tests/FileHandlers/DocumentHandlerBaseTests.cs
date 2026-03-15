using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class DocumentHandlerBaseTests
{
    private class TestHandler(MockFileSystem fs) : DocumentHandlerBase(fs)
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
    }

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
    public void GivenAccessibility_WhenIsPublicAccessibleCalled_ThenReturnsExpected(Accessibility minAccessibility, bool expected)
    {
        TestHandler.TestIsPublicAccessible(minAccessibility).ShouldBe(expected);
    }
}
