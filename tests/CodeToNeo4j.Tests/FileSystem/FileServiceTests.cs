using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileSystem;
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
    [InlineData("src/CodeToNeo4j/Solution/SolutionProcessor.cs", "CodeToNeo4j.Solution.SolutionProcessor", "CodeToNeo4j.Solution")]
    [InlineData("source/Project/File.cs", "Project.File", "Project")]
    [InlineData("Project/File.cs", "Project.File", "Project")]
    [InlineData("File.cs", "File", "")]
    [InlineData("src/SolutionProcessor.cs", "SolutionProcessor", "")]
    [InlineData("src/CodeToNeo4j/Page.razor", "CodeToNeo4j.Page", "CodeToNeo4j")]
    [InlineData("src/CodeToNeo4j/View.xaml", "CodeToNeo4j.View", "CodeToNeo4j")]
    public void InferFileMetadata_Roslyn_ShouldReturnFqnAndDotNamespace(string path, string expectedKey, string expectedNamespace)
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
}
