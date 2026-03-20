using CodeToNeo4j.FileSystem;
using CodeToNeo4j.VersionControl;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.VersionControl;

public class GitLogParserTests
{
	[Theory]
	[InlineData("COMMIT|hash1|#|Author|#|a@b.com|#|2026-01-01T00:00:00Z|#|msg\nM\tfile.cs", 1)]
	[InlineData("", 0)]
	[InlineData("   \n  \n  ", 0)]
	public void GivenGitLogOutput_WhenParseCommitsCalled_ThenReturnsExpectedCount(string output, int expectedCount)
	{
		// Arrange
		var fileService = A.Fake<IFileService>();
		System.IO.Abstractions.FileSystem fileSystem = new();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string s) => s.Replace('\\', '/'));
		GitLogParser sut = new(fileService, fileSystem);

		// Act
		List<CommitMetadata> result = sut.ParseCommits(output, "/repo").ToList();

		// Assert
		result.Count.ShouldBe(expectedCount);
	}

	[Fact]
	public void GivenDeletedFile_WhenParseCommitsCalled_ThenFileStatusIsDeleted()
	{
		// Arrange
		var fileService = A.Fake<IFileService>();
		System.IO.Abstractions.FileSystem fileSystem = new();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string s) => s.Replace('\\', '/'));
		GitLogParser sut = new(fileService, fileSystem);
		var output = "COMMIT|h1|#|Author|#|a@b.com|#|2026-01-01T00:00:00Z|#|delete file\nD\tremoved.cs";

		// Act
		List<CommitMetadata> result = sut.ParseCommits(output, "/repo").ToList();

		// Assert
		result[0].ChangedFiles.ShouldContain(f => f.IsDeleted && f.Path == "/repo/removed.cs");
	}

	[Fact]
	public void GivenSingleCommitHistory_WhenBuildFileMetadataCalled_ThenReturnsCorrectMetadata()
	{
		// Arrange
		var fileService = A.Fake<IFileService>();
		System.IO.Abstractions.FileSystem fileSystem = new();
		GitLogParser sut = new(fileService, fileSystem);
		DateTimeOffset date = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
		List<(string Author, DateTimeOffset Date, string Hash, string? Refs)> history = [("Author One <a@b.com>", date, "abc123", null)];

		// Act
		var result = sut.BuildFileMetadata(history);

		// Assert
		result.Created.ShouldBe(date);
		result.LastModified.ShouldBe(date);
		result.Authors.Count().ShouldBe(1);
		result.Authors.First().Name.ShouldBe("Author One <a@b.com>");
		result.Authors.First().CommitCount.ShouldBe(1);
		result.Commits.ShouldContain("abc123");
	}

	[Fact]
	public void GivenMultipleCommitsWithTags_WhenBuildFileMetadataCalled_ThenExtractsTags()
	{
		// Arrange
		var fileService = A.Fake<IFileService>();
		System.IO.Abstractions.FileSystem fileSystem = new();
		GitLogParser sut = new(fileService, fileSystem);
		DateTimeOffset date1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
		DateTimeOffset date2 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
		List<(string Author, DateTimeOffset Date, string Hash, string? Refs)> history =
			[("Author", date1, "h1", "tag: v1.0"), ("Author", date2, "h2", "tag: v2.0, HEAD -> main")];

		// Act
		var result = sut.BuildFileMetadata(history);

		// Assert
		result.Created.ShouldBe(date1);
		result.LastModified.ShouldBe(date2);
		result.Tags.ShouldContain("v1.0");
		result.Tags.ShouldContain("v2.0");
		result.Authors.First().CommitCount.ShouldBe(2);
	}

	[Theory]
	[InlineData("", 0)]
	[InlineData("   ", 0)]
	public void GivenEmptyOutput_WhenParseSingleFileLogCalled_ThenReturnsEmptyMetadata(string output, int expectedAuthorCount)
	{
		// Arrange
		var fileService = A.Fake<IFileService>();
		System.IO.Abstractions.FileSystem fileSystem = new();
		GitLogParser sut = new(fileService, fileSystem);

		// Act
		var result = sut.ParseSingleFileLog(output);

		// Assert
		result.Authors.Count().ShouldBe(expectedAuthorCount);
		result.Created.ShouldBe(DateTimeOffset.MinValue);
	}

	[Fact]
	public void GivenValidSingleFileLog_WhenParseSingleFileLogCalled_ThenReturnsMetadata()
	{
		// Arrange
		var fileService = A.Fake<IFileService>();
		System.IO.Abstractions.FileSystem fileSystem = new();
		GitLogParser sut = new(fileService, fileSystem);
		var output = "Author One <a@b.com>|2026-03-01T12:00:00Z|abc123|tag: v1.0\nAuthor Two <c@d.com>|2026-02-01T12:00:00Z|def456|";

		// Act
		var result = sut.ParseSingleFileLog(output);

		// Assert
		result.Authors.Count().ShouldBe(2);
		result.Commits.Count().ShouldBe(2);
		result.Tags.ShouldContain("v1.0");
	}
}
