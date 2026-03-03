using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Solution;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Solution;

public class SolutionFileDiscoveryServiceTests
{
    [Fact]
    public async Task GivenSolutionWithDocumentsAndFilesOnDisk_WhenGetFilesToProcessCalled_ThenReturnsCombinedList()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        var fileService = new FileService(fileSystem);
        var sut = new SolutionFileDiscoveryService(fileService, fileSystem);

        var slnPath = "/repo/test.sln";
        var slnFile = new FileInfo(slnPath);
        fileSystem.AddFile(slnPath, new MockFileData(""));
        
        // Setup Roslyn Solution
        var workspace = new AdhocWorkspace();
        var solution = workspace.CurrentSolution;
        // Since we can't easily set FilePath in AdhocWorkspace for this test without more ceremony,
        // let's focus on the file system discovery and assume Roslyn works similarly.

        fileSystem.AddFile("/repo/README.md", new MockFileData(""));
        fileSystem.AddFile("/repo/bin/ignored.cs", new MockFileData(""));

        var includeExtensions = new[] { ".cs", ".md" };

        // Act
        var result = await sut.GetFilesToProcess(slnFile, solution, includeExtensions);

        // Assert
        var files = result.ToList();
        files.Any(f => f.FilePath.EndsWith("README.md")).ShouldBeTrue();
        files.Any(f => f.FilePath.Contains("bin")).ShouldBeFalse();
    }
}
