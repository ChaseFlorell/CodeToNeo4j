using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileSystem;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileSystem;

public class FileServiceTests
{
    private readonly MockFileSystem _fileSystem;
    private readonly FileService _fileService;

    public FileServiceTests()
    {
        _fileSystem = new MockFileSystem();
        _fileService = new FileService(_fileSystem);
    }

    [Theory]
    [InlineData("src/CodeToNeo4j/Solution/SolutionProcessor.cs", "src/CodeToNeo4j/Solution/SolutionProcessor.cs", "CodeToNeo4j.Solution")]
    [InlineData("source/Project/File.cs", "source/Project/File.cs", "Project")]
    [InlineData("Project/File.cs", "Project/File.cs", "Project")]
    [InlineData("File.cs", "File.cs", "")]
    [InlineData("src/SolutionProcessor.cs", "src/SolutionProcessor.cs", "")]
    [InlineData("src/CodeToNeo4j/Page.razor", "src/CodeToNeo4j/Page.razor", "CodeToNeo4j")]
    [InlineData("src/CodeToNeo4j/View.xaml", "src/CodeToNeo4j/View.xaml", "CodeToNeo4j")]
    public void InferFileMetadata_Roslyn_ShouldReturnPathAndDotNamespace(string path, string expectedKey, string expectedNamespace)
    {
        // Act
        var (key, ns) = _fileService.InferFileMetadata(path);

        // Assert
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedNamespace, ns);
    }

    [Theory]
    [InlineData("src/scripts/script.js", "src/scripts/script.js", "src/scripts")]
    [InlineData("config.json", "config.json", "")]
    [InlineData("styles/main.css", "styles/main.css", "styles")]
    public void InferFileMetadata_NonRoslyn_ShouldReturnPathAndSlashNamespace(string path, string expectedKey, string expectedNamespace)
    {
        // Act
        var (key, ns) = _fileService.InferFileMetadata(path);

        // Assert
        Assert.Equal(expectedKey, key);
        Assert.Equal(expectedNamespace, ns);
    }

    [Theory]
    [InlineData("/repo/src/File.cs", "/repo", "src/File.cs")]
    [InlineData("/repo/lib/a/b.ts", "/repo", "lib/a/b.ts")]
    public void GivenAbsolutePaths_WhenGetRelativePathCalled_ThenReturnsForwardSlashRelativePath(
        string path, string relativeTo, string expected)
    {
        _fileService.GetRelativePath(relativeTo, path).ShouldBe(expected);
    }

    [Fact]
    public async Task GivenFileWithKnownContent_WhenComputeSha256Called_ThenReturnsCorrectHash()
    {
        // Arrange
        var fs = new MockFileSystem();
        fs.AddFile("/test/file.txt", new MockFileData("hello"));
        var sut = new FileService(fs);

        // Act
        var hash = await sut.ComputeSha256("/test/file.txt");

        // Assert — SHA-256 of "hello" (UTF-8) is well-known
        hash.ShouldBe("2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824");
    }
}
