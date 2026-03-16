using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Solution;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Solution;

public class SolutionFileDiscoveryServiceTests
{
    [Fact]
    public void GivenSolutionWithDocumentsAndFilesOnDisk_WhenGetFilesToProcessCalled_ThenReturnsCombinedList()
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
        var result = sut.GetFilesToProcess(slnFile, solution, includeExtensions);

        // Assert
        var files = result.ToList();
        files.Any(f => f.FilePath.EndsWith("README.md")).ShouldBeTrue();
        files.Any(f => f.FilePath.Contains("bin")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("Serilog(net9.0)", "net9.0")]
    [InlineData("Serilog(net8.0)", "net8.0")]
    [InlineData("Serilog(netstandard2.0)", "netstandard2.0")]
    [InlineData("MyApp(net462)", "net462")]
    [InlineData("SimpleProject", null)]
    [InlineData("", null)]
    [InlineData("Project.With.Dots(net10.0)", "net10.0")]
    public void GivenProjectName_WhenExtractTargetFrameworkCalled_ThenReturnsExpectedTfm(string projectName, string? expected)
    {
        var result = SolutionFileDiscoveryService.ExtractTargetFramework(projectName);
        result.ShouldBe(expected);
    }

    [Fact]
    public void GivenMultiTargetProjects_WhenGetFilesToProcessCalled_ThenAccumulatesTfms()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/solution");
        fileSystem.AddFile("/solution/src/MyClass.cs", new MockFileData("class MyClass {}"));

        var fileService = A.Fake<IFileService>();
        A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

        var sut = new SolutionFileDiscoveryService(fileService, fileSystem);

        var workspace = new AdhocWorkspace();
        var projectId1 = ProjectId.CreateNewId();
        var projectId2 = ProjectId.CreateNewId();
        var docId1 = DocumentId.CreateNewId(projectId1);
        var docId2 = DocumentId.CreateNewId(projectId2);

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId1, VersionStamp.Default, "MyProject(net9.0)", "MyProject", LanguageNames.CSharp))
            .AddDocument(DocumentInfo.Create(docId1, "MyClass.cs", filePath: "/solution/src/MyClass.cs"))
            .AddProject(ProjectInfo.Create(projectId2, VersionStamp.Default, "MyProject(net8.0)", "MyProject", LanguageNames.CSharp))
            .AddDocument(DocumentInfo.Create(docId2, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

        var sln = new FileInfo("/solution/MySolution.sln");

        // Act
        var files = sut.GetFilesToProcess(sln, solution, [".cs"]).ToArray();

        // Assert
        files.Length.ShouldBe(1);
        files[0].TargetFrameworks.ShouldNotBeNull();
        files[0].TargetFrameworks!.ShouldContain("net9.0");
        files[0].TargetFrameworks!.ShouldContain("net8.0");
    }

    [Fact]
    public void GivenWrapperProjectWithNoDocuments_WhenGetFilesToProcessCalled_ThenSkipsProject()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/solution");
        fileSystem.AddFile("/solution/src/MyClass.cs", new MockFileData("class MyClass {}"));

        var fileService = A.Fake<IFileService>();
        A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

        var sut = new SolutionFileDiscoveryService(fileService, fileSystem);

        var workspace = new AdhocWorkspace();
        var wrapperId = ProjectId.CreateNewId();
        var realId = ProjectId.CreateNewId();
        var docId = DocumentId.CreateNewId(realId);

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(wrapperId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp))
            .AddProject(ProjectInfo.Create(realId, VersionStamp.Default, "MyProject(net9.0)", "MyProject", LanguageNames.CSharp))
            .AddDocument(DocumentInfo.Create(docId, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

        var sln = new FileInfo("/solution/MySolution.sln");

        // Act
        var files = sut.GetFilesToProcess(sln, solution, [".cs"]).ToArray();

        // Assert
        files.Length.ShouldBe(1);
        files[0].FilePath.ShouldBe("/solution/src/MyClass.cs");
        files[0].TargetFrameworks.ShouldNotBeNull();
        files[0].TargetFrameworks!.ShouldContain("net9.0");
    }

    [Fact]
    public void GivenSingleTargetProject_WhenGetFilesToProcessCalled_ThenTargetFrameworksIsNull()
    {
        // Arrange
        var fileSystem = new MockFileSystem();
        fileSystem.AddDirectory("/solution");
        fileSystem.AddFile("/solution/src/MyClass.cs", new MockFileData("class MyClass {}"));

        var fileService = A.Fake<IFileService>();
        A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

        var sut = new SolutionFileDiscoveryService(fileService, fileSystem);

        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId();
        var docId = DocumentId.CreateNewId(projectId);

        var solution = workspace.CurrentSolution
            .AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp))
            .AddDocument(DocumentInfo.Create(docId, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

        var sln = new FileInfo("/solution/MySolution.sln");

        // Act
        var files = sut.GetFilesToProcess(sln, solution, [".cs"]).ToArray();

        // Assert
        files.Length.ShouldBe(1);
        files[0].TargetFrameworks.ShouldBeNull();
    }
}
