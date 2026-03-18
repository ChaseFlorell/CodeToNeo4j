using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.ProgramOptions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.ProgramOptions;

public class InputPathResolverTests
{
    // ── Auto-detection ───────────────────────────────────────────────────────

    [Fact]
    public void GivenSingleSlnFile_WhenAutoDetecting_ThenReturnsThatFile()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MySolution.sln", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo/MySolution.sln");
    }

    [Fact]
    public void GivenSingleSlnxFile_WhenAutoDetecting_ThenReturnsThatFile()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MySolution.slnx", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo/MySolution.slnx");
    }

    [Fact]
    public void GivenSingleCsprojFile_WhenAutoDetecting_ThenReturnsThatFile()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MyProject.csproj", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo/MyProject.csproj");
    }

    [Fact]
    public void GivenPubspecYaml_WhenAutoDetecting_ThenReturnsDirectory()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/pubspec.yaml", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo");
    }

    [Fact]
    public void GivenNoProjectFiles_WhenAutoDetecting_ThenReturnsDirectoryForFilesOnlyMode()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddDirectory("/repo");
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo");
    }

    [Fact]
    public void GivenMultipleSlnFiles_WhenAutoDetecting_ThenThrowsWithClearMessage()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/A.sln", new MockFileData(""));
        fs.AddFile("/repo/B.sln", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act & assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.AutoDetect("/repo"));
        ex.Message.ShouldContain("Multiple .sln files");
        ex.Message.ShouldContain("--input");
    }

    [Fact]
    public void GivenMultipleSlnxFiles_WhenAutoDetecting_ThenThrowsWithClearMessage()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/A.slnx", new MockFileData(""));
        fs.AddFile("/repo/B.slnx", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act & assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.AutoDetect("/repo"));
        ex.Message.ShouldContain("Multiple .slnx files");
        ex.Message.ShouldContain("--input");
    }

    [Fact]
    public void GivenMultipleCsprojFiles_WhenAutoDetecting_ThenThrowsWithClearMessage()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/A.csproj", new MockFileData(""));
        fs.AddFile("/repo/B.csproj", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act & assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.AutoDetect("/repo"));
        ex.Message.ShouldContain("Multiple .csproj files");
        ex.Message.ShouldContain("--input");
    }

    [Fact]
    public void GivenSlnAndCsproj_WhenAutoDetecting_ThenSlnWins()
    {
        // arrange — priority: .sln > .csproj
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MySolution.sln", new MockFileData(""));
        fs.AddFile("/repo/MyProject.csproj", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo/MySolution.sln");
    }

    [Fact]
    public void GivenSlnxAndCsproj_WhenAutoDetecting_ThenSlnxWins()
    {
        // arrange — priority: .slnx > .csproj
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MySolution.slnx", new MockFileData(""));
        fs.AddFile("/repo/MyProject.csproj", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo/MySolution.slnx");
    }

    [Fact]
    public void GivenCsprojAndPubspec_WhenAutoDetecting_ThenCsprojWins()
    {
        // arrange — priority: .csproj > pubspec.yaml
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MyProject.csproj", new MockFileData(""));
        fs.AddFile("/repo/pubspec.yaml", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.AutoDetect("/repo");

        // assert
        result.ShouldBe("/repo/MyProject.csproj");
    }

    // ── Explicit path ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("test.sln")]
    [InlineData("test.slnx")]
    [InlineData("test.csproj")]
    public void GivenExplicitFile_WhenResolving_ThenReturnsFullPath(string fileName)
    {
        // arrange
        var filePath = $"/repo/{fileName}";
        var fs = new MockFileSystem();
        fs.AddFile(filePath, new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.ResolveExplicit(filePath);

        // assert
        result.ShouldBe(fs.Path.GetFullPath(filePath));
    }

    [Fact]
    public void GivenExplicitDirectory_WhenResolving_ThenReturnsDirectoryPath()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddDirectory("/repo");
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.ResolveExplicit("/repo");

        // assert
        result.ShouldBe(fs.Path.GetFullPath("/repo"));
    }

    [Fact]
    public void GivenNonExistentPath_WhenResolving_ThenThrowsWithClearMessage()
    {
        // arrange
        var fs = new MockFileSystem();
        var sut = new InputPathResolver(fs);

        // act & assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.ResolveExplicit("/repo/does-not-exist.sln"));
        ex.Message.ShouldContain("does not exist");
    }

    [Theory]
    [InlineData(".txt")]
    [InlineData(".cs")]
    [InlineData(".xml")]
    public void GivenUnsupportedExtension_WhenResolving_ThenThrowsWithClearMessage(string ext)
    {
        // arrange
        var filePath = $"/repo/file{ext}";
        var fs = new MockFileSystem();
        fs.AddFile(filePath, new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act & assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.ResolveExplicit(filePath));
        ex.Message.ShouldContain("Unsupported file type");
        ex.Message.ShouldContain(".sln");
        ex.Message.ShouldContain(".slnx");
        ex.Message.ShouldContain(".csproj");
    }

    // ── Resolve (top-level) ──────────────────────────────────────────────────

    [Fact]
    public void GivenExplicitSlnPath_WhenResolving_ThenReturnsFullPath()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MyApp.sln", new MockFileData(""));
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.Resolve("/repo/MyApp.sln");

        // assert
        result.ShouldBe(fs.Path.GetFullPath("/repo/MyApp.sln"));
    }

    [Fact]
    public void GivenNullPath_WhenResolving_ThenAutoDetectsFromCwd()
    {
        // arrange
        var fs = new MockFileSystem();
        fs.AddFile("/cwd/Solution.sln", new MockFileData(""));
        fs.Directory.SetCurrentDirectory("/cwd");
        var sut = new InputPathResolver(fs);

        // act
        var result = sut.Resolve(null);

        // assert
        result.ShouldBe("/cwd/Solution.sln");
    }

    // ── RepoKey derivation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("MySolution.sln", "mysolution")]
    [InlineData("MyProject.csproj", "myproject")]
    [InlineData("MySolution.slnx", "mysolution")]
    public void GivenFileInputPath_WhenGettingRepoKey_ThenDerivedFromFileName(string inputPath, string expectedKey)
    {
        // arrange
        var options = CreateOptions(inputPath, expectedKey);

        // act & assert
        options.RepoKey.ShouldBe(expectedKey);
    }

    [Theory]
    [InlineData("/path/to/project", "project")]
    [InlineData("/path/to/my-app", "my-app")]
    public void GivenDirectoryInputPath_WhenGettingRepoKey_ThenDerivedFromDirectoryName(string inputPath, string expectedKey)
    {
        // arrange
        var options = CreateOptions(inputPath, expectedKey);

        // act & assert
        options.RepoKey.ShouldBe(expectedKey);
    }

    [Fact]
    public void GivenNoKey_WhenGettingRepoKey_ThenReturnsNull()
    {
        // arrange
        var options = CreateOptions("test.sln", repoKey: null);

        // act & assert
        options.RepoKey.ShouldBeNull();
    }

    private static Options CreateOptions(string inputPath, string? repoKey = "test")
    {
        var fs = new MockFileSystem();
        IFileSystemInfo fsi = inputPath.Contains('.')
            ? fs.FileInfo.New(inputPath)
            : fs.DirectoryInfo.New(inputPath);
        return new Options(
            fsi,
            repoKey,
        "bolt://localhost",
        "user",
        "pass",
        repoKey is null,
        null,
        100,
        "neo4j",
        Microsoft.Extensions.Logging.LogLevel.Information,
        false,
        Microsoft.CodeAnalysis.Accessibility.Private,
        [],
        false,
        ShowVersion: false,
        ShowSupportedFiles: false,
        ShowInfo: false);
    }
}
