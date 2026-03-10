using CodeToNeo4j.FileSystem;
using CodeToNeo4j.VersionControl;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.VersionControl;

public class GitServiceTests
{
    [Fact]
    public void GivenValidGitLogOutput_WhenParseCommitsCalled_ThenReturnsExpectedCommits()
    {
        // Arrange
        var logger = A.Fake<ILogger<GitService>>();
        var fileSystem = new System.IO.Abstractions.FileSystem();
        var fileService = A.Fake<IFileService>();
        var sut = new GitService(fileService, fileSystem, logger);

        var repoRoot = "/repo";
        var output = @"COMMIT|hash1|#|Author One|#|author1@example.com|#|2026-03-09T14:20:00Z|#|Commit Message One
file1.cs
file2.cs
COMMIT|hash2|#|Author Two|#|author2@example.com|#|2026-03-09T14:21:00Z|#|Commit Message Two
file3.cs";

        // Mock NormalizePath to just return the input for simplicity
        A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string s) => s.Replace('\\', '/'));

        // Act
        var result = sut.ParseCommits(output, repoRoot).ToList();

        // Assert
        result.Count.ShouldBe(2);

        result[0].Hash.ShouldBe("hash1");
        result[0].AuthorName.ShouldBe("Author One");
        result[0].AuthorEmail.ShouldBe("author1@example.com");
        result[0].Message.ShouldBe("Commit Message One");
        result[0].ChangedFiles.ShouldBe(new[] { "/repo/file1.cs", "/repo/file2.cs" });

        result[1].Hash.ShouldBe("hash2");
        result[1].AuthorName.ShouldBe("Author Two");
        result[1].AuthorEmail.ShouldBe("author2@example.com");
        result[1].Message.ShouldBe("Commit Message Two");
        result[1].ChangedFiles.ShouldBe(new[] { "/repo/file3.cs" });
    }
}
