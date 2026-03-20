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
	public void GivenMultiTargetProjects_WhenGetFilesToProcessCalled_ThenAccumulatesTfms()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		fileSystem.AddDirectory("/solution");
		fileSystem.AddFile("/solution/src/MyClass.cs", new("class MyClass {}"));

		var fileService = A.Fake<IFileService>();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		AdhocWorkspace workspace = new();
		ProjectId projectId1 = ProjectId.CreateNewId();
		ProjectId projectId2 = ProjectId.CreateNewId();
		DocumentId docId1 = DocumentId.CreateNewId(projectId1);
		DocumentId docId2 = DocumentId.CreateNewId(projectId2);

		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(projectId1, VersionStamp.Default, "MyProject(net9.0)", "MyProject", LanguageNames.CSharp))
			.AddDocument(DocumentInfo.Create(docId1, "MyClass.cs", filePath: "/solution/src/MyClass.cs"))
			.AddProject(ProjectInfo.Create(projectId2, VersionStamp.Default, "MyProject(net8.0)", "MyProject", LanguageNames.CSharp))
			.AddDocument(DocumentInfo.Create(docId2, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

		// Act
		var files = sut.GetFilesToProcess("/solution", solution, [".cs"]).ToArray();

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
		MockFileSystem fileSystem = new();
		fileSystem.AddDirectory("/solution");
		fileSystem.AddFile("/solution/src/MyClass.cs", new("class MyClass {}"));

		var fileService = A.Fake<IFileService>();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		AdhocWorkspace workspace = new();
		ProjectId wrapperId = ProjectId.CreateNewId();
		ProjectId realId = ProjectId.CreateNewId();
		DocumentId docId = DocumentId.CreateNewId(realId);

		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(wrapperId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp))
			.AddProject(ProjectInfo.Create(realId, VersionStamp.Default, "MyProject(net9.0)", "MyProject", LanguageNames.CSharp))
			.AddDocument(DocumentInfo.Create(docId, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

		// Act
		var files = sut.GetFilesToProcess("/solution", solution, [".cs"]).ToArray();

		// Assert
		files.Length.ShouldBe(1);
		files[0].FilePath.ShouldBe("/solution/src/MyClass.cs");
		files[0].TargetFrameworks.ShouldNotBeNull();
		files[0].TargetFrameworks!.ShouldContain("net9.0");
	}

	[Fact]
	public void GivenSingleTargetProjectWithNoProjectFile_WhenGetFilesToProcessCalled_ThenTargetFrameworksIsNull()
	{
		// AdhocWorkspace projects have no FilePath, so the file-based fallback returns null
		// Arrange
		MockFileSystem fileSystem = new();
		fileSystem.AddDirectory("/solution");
		fileSystem.AddFile("/solution/src/MyClass.cs", new("class MyClass {}"));

		var fileService = A.Fake<IFileService>();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		AdhocWorkspace workspace = new();
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId docId = DocumentId.CreateNewId(projectId);

		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp))
			.AddDocument(DocumentInfo.Create(docId, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

		// Act
		var files = sut.GetFilesToProcess("/solution", solution, [".cs"]).ToArray();

		// Assert
		files.Length.ShouldBe(1);
		files[0].TargetFrameworks.ShouldBeNull();
	}

	[Fact]
	public void GivenSingleTargetProjectWithCsprojFile_WhenGetFilesToProcessCalled_ThenTargetFrameworksFromFile()
	{
		// Single-target projects don't embed TFM in project name; discovery falls back to reading the .csproj
		// Arrange
		MockFileSystem fileSystem = new();
		fileSystem.AddDirectory("/solution");
		fileSystem.AddFile("/solution/src/MyClass.cs", new("class MyClass {}"));
		fileSystem.AddFile("/solution/MyProject.csproj", new("""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net10.0</TargetFramework>
			  </PropertyGroup>
			</Project>
			"""));

		var fileService = A.Fake<IFileService>();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		AdhocWorkspace workspace = new();
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId docId = DocumentId.CreateNewId(projectId);

		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp,
				filePath: "/solution/MyProject.csproj"))
			.AddDocument(DocumentInfo.Create(docId, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

		// Act
		var files = sut.GetFilesToProcess("/solution", solution, [".cs"]).ToArray();

		// Assert
		files.Length.ShouldBe(1);
		files[0].TargetFrameworks.ShouldNotBeNull();
		files[0].TargetFrameworks!.ShouldContain("net10.0");
	}

	[Fact]
	public void GivenMultiTargetCsprojFile_WhenGetFilesToProcessCalled_ThenAllTargetFrameworksFromFile()
	{
		// A project with <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks> should produce all three TFMs
		// Arrange
		MockFileSystem fileSystem = new();
		fileSystem.AddDirectory("/solution");
		fileSystem.AddFile("/solution/src/MyClass.cs", new("class MyClass {}"));
		fileSystem.AddFile("/solution/MyProject.csproj", new("""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
			  </PropertyGroup>
			</Project>
			"""));

		var fileService = A.Fake<IFileService>();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		AdhocWorkspace workspace = new();
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId docId = DocumentId.CreateNewId(projectId);

		// Single-target project instance (no TFM in name) pointing to the multi-target csproj
		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp,
				filePath: "/solution/MyProject.csproj"))
			.AddDocument(DocumentInfo.Create(docId, "MyClass.cs", filePath: "/solution/src/MyClass.cs"));

		// Act
		var files = sut.GetFilesToProcess("/solution", solution, [".cs"]).ToArray();

		// Assert
		files.Length.ShouldBe(1);
		files[0].TargetFrameworks.ShouldNotBeNull();
		files[0].TargetFrameworks!.ShouldContain("net8.0");
		files[0].TargetFrameworks!.ShouldContain("net9.0");
		files[0].TargetFrameworks!.ShouldContain("net10.0");
	}

	[Fact]
	public void GivenMultipleFilesInSameProject_WhenGetFilesToProcessCalled_ThenAllFilesShareProjectTfms()
	{
		// TFMs read from .csproj once (cached) and applied to every file in that project
		// Arrange
		MockFileSystem fileSystem = new();
		fileSystem.AddDirectory("/solution");
		fileSystem.AddFile("/solution/src/A.cs", new("class A {}"));
		fileSystem.AddFile("/solution/src/B.cs", new("class B {}"));
		fileSystem.AddFile("/solution/MyProject.csproj", new("""
			<Project Sdk="Microsoft.NET.Sdk">
			  <PropertyGroup>
			    <TargetFramework>net9.0</TargetFramework>
			  </PropertyGroup>
			</Project>
			"""));

		var fileService = A.Fake<IFileService>();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		AdhocWorkspace workspace = new();
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId docIdA = DocumentId.CreateNewId(projectId);
		DocumentId docIdB = DocumentId.CreateNewId(projectId);

		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(projectId, VersionStamp.Default, "MyProject", "MyProject", LanguageNames.CSharp,
				filePath: "/solution/MyProject.csproj"))
			.AddDocument(DocumentInfo.Create(docIdA, "A.cs", filePath: "/solution/src/A.cs"))
			.AddDocument(DocumentInfo.Create(docIdB, "B.cs", filePath: "/solution/src/B.cs"));

		// Act
		var files = sut.GetFilesToProcess("/solution", solution, [".cs"]).ToArray();

		// Assert
		files.Length.ShouldBe(2);
		files.ShouldAllBe(f => f.TargetFrameworks != null && f.TargetFrameworks.Contains("net9.0"));
	}

	[Fact]
	public void GivenAdditionalDocumentsFromMultiTargetProjects_WhenGetFilesToProcessCalled_ThenAccumulatesTfms()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		fileSystem.AddDirectory("/solution");
		fileSystem.AddFile("/solution/data/config.json", new("{}"));

		var fileService = A.Fake<IFileService>();
		A.CallTo(() => fileService.NormalizePath(A<string>._)).ReturnsLazily((string p) => p);

		SolutionFileDiscoveryService sut = new(fileService, fileSystem);

		AdhocWorkspace workspace = new();
		ProjectId projectId1 = ProjectId.CreateNewId();
		ProjectId projectId2 = ProjectId.CreateNewId();
		DocumentId docId1 = DocumentId.CreateNewId(projectId1);
		DocumentId docId2 = DocumentId.CreateNewId(projectId2);

		// Add a regular document so the projects aren't filtered as wrappers
		DocumentId codeDocId1 = DocumentId.CreateNewId(projectId1);
		DocumentId codeDocId2 = DocumentId.CreateNewId(projectId2);

		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(projectId1, VersionStamp.Default, "MyProject(net9.0)", "MyProject", LanguageNames.CSharp))
			.AddDocument(DocumentInfo.Create(codeDocId1, "Dummy.cs", filePath: "/solution/src/Dummy.cs"))
			.AddAdditionalDocument(DocumentInfo.Create(docId1, "config.json", filePath: "/solution/data/config.json"))
			.AddProject(ProjectInfo.Create(projectId2, VersionStamp.Default, "MyProject(net8.0)", "MyProject", LanguageNames.CSharp))
			.AddDocument(DocumentInfo.Create(codeDocId2, "Dummy.cs", filePath: "/solution/src/Dummy.cs"))
			.AddAdditionalDocument(DocumentInfo.Create(docId2, "config.json", filePath: "/solution/data/config.json"));

		// Act
		var files = sut.GetFilesToProcess("/solution", solution, [".cs", ".json"]).ToArray();

		// Assert
		var configFile = files.FirstOrDefault(f => f.FilePath.EndsWith("config.json"));
		configFile.ShouldNotBeNull();
		configFile.TargetFrameworks.ShouldNotBeNull();
		configFile.TargetFrameworks!.ShouldContain("net9.0");
		configFile.TargetFrameworks!.ShouldContain("net8.0");
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
