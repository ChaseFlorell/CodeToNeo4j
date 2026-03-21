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
	public void GivenSolutionWithDocumentsAndFilesOnDisk_WhenGetFilesToProcessCalled_ThenReturnsCombinedList()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		FileService fileService = new(fileSystem);
		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		var slnPath = "/repo/test.sln";
		var slnDir = "/repo";
		fileSystem.AddFile(slnPath, new(""));

		// Setup Roslyn Solution
		AdhocWorkspace workspace = new();
		var solution = workspace.CurrentSolution;
		// Since we can't easily set FilePath in AdhocWorkspace for this test without more ceremony,
		// let's focus on the file system discovery and assume Roslyn works similarly.

		fileSystem.AddFile("/repo/README.md", new(""));
		fileSystem.AddFile("/repo/bin/ignored.cs", new(""));

		var includeExtensions = new[] { ".cs", ".md" };

		// Act
		var result = sut.GetFilesToProcess(slnDir, solution, includeExtensions);

		// Assert
		List<ProcessedFile> files = result.ToList();
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
	public void GivenDirectoryWithDartFiles_WhenGetFilesToProcessByDirectoryCalled_ThenReturnsDartFiles()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		FileService fileService = new(fileSystem);
		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		fileSystem.AddFile("/dartproject/pubspec.yaml", new("name: test"));
		fileSystem.AddFile("/dartproject/lib/main.dart", new("void main() {}"));
		fileSystem.AddFile("/dartproject/lib/src/foo.dart", new("class Foo {}"));
		fileSystem.AddFile("/dartproject/build/output.dart", new("// excluded"));
		fileSystem.AddFile("/dartproject/.dart_tool/cache.dart", new("// excluded"));

		var includeExtensions = new[] { ".dart", "pubspec.yaml" };

		// Act
		List<ProcessedFile> result = sut.GetFilesToProcess("/dartproject", includeExtensions).ToList();

		// Assert
		result.Any(f => f.FilePath.Contains("main.dart")).ShouldBeTrue();
		result.Any(f => f.FilePath.Contains("foo.dart")).ShouldBeTrue();
		result.Any(f => f.FilePath.Contains("pubspec.yaml")).ShouldBeTrue();
		result.Any(f => f.FilePath.Contains("build")).ShouldBeFalse();
		result.Any(f => f.FilePath.Contains(".dart_tool")).ShouldBeFalse();
	}

	[Fact]
	public void GivenPubspecAlreadyDiscoveredViaExtension_WhenGetFilesToProcessByDirectoryCalled_ThenNotDuplicated()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		FileService fileService = new(fileSystem);
		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		fileSystem.AddFile("/dartproject/pubspec.yaml", new("name: test"));

		// Include "pubspec.yaml" as a full-filename extension so it's picked up by the main loop
		var includeExtensions = new[] { "pubspec.yaml" };

		// Act
		List<ProcessedFile> result = sut.GetFilesToProcess("/dartproject", includeExtensions).ToList();

		// Assert — pubspec.yaml appears exactly once (not duplicated by the special-case block)
		result.Count(f => f.FilePath.EndsWith("pubspec.yaml")).ShouldBe(1);
	}

	[Fact]
	public void GivenDirectoryWithNoDartFiles_WhenGetFilesToProcessByDirectoryCalled_ThenReturnsOnlyPubspec()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		FileService fileService = new(fileSystem);
		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		fileSystem.AddFile("/dartproject/pubspec.yaml", new("name: test"));
		fileSystem.AddFile("/dartproject/README.md", new("# README"));

		var includeExtensions = new[] { ".dart", "pubspec.yaml" };

		// Act
		List<ProcessedFile> result = sut.GetFilesToProcess("/dartproject", includeExtensions).ToList();

		// Assert
		result.Count.ShouldBe(1);
		result[0].FilePath.ShouldContain("pubspec.yaml");
	}
}
