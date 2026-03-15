using CodeToNeo4j.FileSystem;
using CodeToNeo4j.VersionControl;
using FakeItEasy;
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
        var fileSystem = new System.IO.Abstractions.FileSystem();
        var fileService = A.Fake<IFileService>();
        var sut = new GitLogParser(fileService, fileSystem);

        var repoRoot = "/repo";
        var output = @"COMMIT|hash1|#|Author One|#|author1@example.com|#|2026-03-09T14:20:00Z|#|Commit Message One
M	file1.cs
A	file2.cs
COMMIT|hash2|#|Author Two|#|author2@example.com|#|2026-03-09T14:21:00Z|#|Commit Message Two
D	file3.cs";

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
        result[0].ChangedFiles.Count().ShouldBe(2);
        result[0].ChangedFiles.ShouldContain(f => f.Path == "/repo/file1.cs" && !f.IsDeleted);
        result[0].ChangedFiles.ShouldContain(f => f.Path == "/repo/file2.cs" && !f.IsDeleted);

        result[1].Hash.ShouldBe("hash2");
        result[1].AuthorName.ShouldBe("Author Two");
        result[1].AuthorEmail.ShouldBe("author2@example.com");
        result[1].Message.ShouldBe("Commit Message Two");
        result[1].ChangedFiles.Count().ShouldBe(1);
        result[1].ChangedFiles.ShouldContain(f => f.Path == "/repo/file3.cs" && f.IsDeleted);
    }

    [Fact]
    public void GivenEmptyOutput_WhenParseCommitsCalled_ThenReturnsEmptyList()
    {
        // Arrange
        var sut = CreateParser();

        // Act
        var result = sut.ParseCommits("", "/repo").ToList();

        // Assert
        result.ShouldBeEmpty();
    }

    [Fact]
    public void GivenCommitWithNoFiles_WhenParseCommitsCalled_ThenReturnsCommitWithEmptyChangedFiles()
    {
        // Arrange
        var sut = CreateParser();
        var output = "COMMIT|abc123|#|Author|#|author@test.com|#|2026-03-10T10:00:00Z|#|Empty commit";

        // Act
        var result = sut.ParseCommits(output, "/repo").ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].Hash.ShouldBe("abc123");
        result[0].ChangedFiles.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("R100\told.cs\tnew.cs", false)]
    [InlineData("C100\tsource.cs\tcopy.cs", false)]
    public void GivenRenameOrCopyStatus_WhenParseCommitsCalled_ThenTreatsAsNonDeleted(string fileLine, bool expectedDeleted)
    {
        // Arrange
        var (sut, fileService) = CreateParserWithFileService();
        A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string s) => s.Replace('\\', '/'));

        var output = $"COMMIT|abc123|#|Author|#|author@test.com|#|2026-03-10T10:00:00Z|#|Rename commit\n{fileLine}";

        // Act
        var result = sut.ParseCommits(output, "/repo").ToList();

        // Assert
        result.Count.ShouldBe(1);
        result[0].ChangedFiles.Any(f => f.IsDeleted == expectedDeleted).ShouldBeTrue();
    }

    [Fact]
    public void GivenMultipleConsecutiveCommits_WhenParseCommitsCalled_ThenParsesAllCorrectly()
    {
        // Arrange
        var (sut, fileService) = CreateParserWithFileService();
        A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string s) => s.Replace('\\', '/'));

        var output = """
            COMMIT|hash1|#|Alice|#|alice@test.com|#|2026-01-01T00:00:00Z|#|First
            M	a.cs
            COMMIT|hash2|#|Bob|#|bob@test.com|#|2026-01-02T00:00:00Z|#|Second
            COMMIT|hash3|#|Charlie|#|charlie@test.com|#|2026-01-03T00:00:00Z|#|Third
            D	b.cs
            A	c.cs
            """;

        // Act
        var result = sut.ParseCommits(output, "/repo").ToList();

        // Assert
        result.Count.ShouldBe(3);
        result[0].Hash.ShouldBe("hash1");
        result[0].ChangedFiles.Count().ShouldBe(1);
        result[1].Hash.ShouldBe("hash2");
        result[1].ChangedFiles.ShouldBeEmpty();
        result[2].Hash.ShouldBe("hash3");
        result[2].ChangedFiles.Count().ShouldBe(2);
    }

    [Fact]
    public void GivenMalformedCommitHeader_WhenParseCommitsCalled_ThenSkipsIt()
    {
        // Arrange
        var sut = CreateParser();
        var output = "COMMIT|incomplete";

        // Act
        var result = sut.ParseCommits(output, "/repo").ToList();

        // Assert
        result.ShouldBeEmpty();
    }

    private static GitLogParser CreateParser()
    {
        var fileService = A.Fake<IFileService>();
        A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string s) => s.Replace('\\', '/'));
        return new GitLogParser(fileService, new System.IO.Abstractions.FileSystem());
    }

    private static (GitLogParser Sut, IFileService FileService) CreateParserWithFileService()
    {
        var fileService = A.Fake<IFileService>();
        var sut = new GitLogParser(fileService, new System.IO.Abstractions.FileSystem());
        return (sut, fileService);
    }
}
