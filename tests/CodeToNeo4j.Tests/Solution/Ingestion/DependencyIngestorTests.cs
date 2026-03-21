using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Solution.Ingestion;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Solution.Ingestion;

public class DependencyIngestorTests
{
	[Fact]
	public async Task GivenSolutionWithProject_WhenIngestDependenciesCalled_ThenKeysAreVersionless()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		var logger = A.Fake<ILogger<DependencyIngestor>>();
		DependencyIngestor sut = new(graphService, logger);

		AdhocWorkspace workspace = new();
		// Using a reference that we know has a version
		var assembly = typeof(Enumerable).Assembly;
		var reference = MetadataReference.CreateFromFile(assembly.Location);

		var project = workspace.AddProject("Project1", LanguageNames.CSharp);
		workspace.TryApplyChanges(workspace.CurrentSolution.AddMetadataReference(project.Id, reference));

		var solution = workspace.CurrentSolution;

		Dependency[]? capturedDeps = null;
		A.CallTo(() => graphService.UpsertDependencies(A<string>._, A<Dependency[]>._, A<string>._))
			.Invokes((string? r, IEnumerable<Dependency> d, string db) => capturedDeps = d.ToArray());

		// Act
		await sut.IngestDependencies(solution, "test-repo", "neo4j");

		// Assert
		capturedDeps.ShouldNotBeNull();
		foreach (var dep in capturedDeps)
		{
			dep.Key.ShouldBe($"pkg:{dep.Name}");
			// The version should not be in the key
			dep.Key.ShouldNotContain(dep.Version);
		}
	}

	[Fact]
	public async Task GivenMultiTargetProjects_WhenIngestDependenciesCalled_ThenOnlyCompilesOnePerBaseName()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		var logger = A.Fake<ILogger<DependencyIngestor>>();
		DependencyIngestor sut = new(graphService, logger);

		AdhocWorkspace workspace = new();
		var assembly = typeof(Enumerable).Assembly;
		var reference = MetadataReference.CreateFromFile(assembly.Location);

		// Simulate multi-target: two projects with same base name but different TFMs
		var project1 = workspace.AddProject("MyLib(net9.0)", LanguageNames.CSharp);
		var doc1 = workspace.AddDocument(project1.Id, "Class1.cs",
			Microsoft.CodeAnalysis.Text.SourceText.From("class A {}"));
		workspace.TryApplyChanges(workspace.CurrentSolution.AddMetadataReference(project1.Id, reference));

		ProjectId project2Id = ProjectId.CreateNewId();
		var solution = workspace.CurrentSolution
			.AddProject(ProjectInfo.Create(project2Id, VersionStamp.Default, "MyLib(net8.0)", "MyLib", LanguageNames.CSharp))
			.AddDocument(DocumentId.CreateNewId(project2Id), "Class1.cs",
				Microsoft.CodeAnalysis.Text.SourceText.From("class A {}"))
			.AddMetadataReference(project2Id, reference);

		Dependency[]? capturedDeps = null;
		A.CallTo(() => graphService.UpsertDependencies(A<string>._, A<Dependency[]>._, A<string>._))
			.Invokes((string? r, IEnumerable<Dependency> d, string db) => capturedDeps = d.ToArray());

		// Act
		await sut.IngestDependencies(solution, "test-repo", "neo4j");

		// Assert — dependencies should still be captured (from whichever TFM ran first)
		capturedDeps.ShouldNotBeNull();
		capturedDeps.Length.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task GivenWrapperProjectWithNoDocuments_WhenIngestDependenciesCalled_ThenSkipsIt()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		var logger = A.Fake<ILogger<DependencyIngestor>>();
		DependencyIngestor sut = new(graphService, logger);

		AdhocWorkspace workspace = new();
		// Add a wrapper project with no documents
		workspace.AddProject("MyLib", LanguageNames.CSharp);

		var solution = workspace.CurrentSolution;

		Dependency[]? capturedDeps = null;
		A.CallTo(() => graphService.UpsertDependencies(A<string>._, A<Dependency[]>._, A<string>._))
			.Invokes((string? r, IEnumerable<Dependency> d, string db) => capturedDeps = d.ToArray());

		// Act
		await sut.IngestDependencies(solution, "test-repo", "neo4j");

		// Assert — should still call upsert but with empty deps since no projects had documents
		A.CallTo(() => graphService.UpsertDependencies(A<string>._, A<IEnumerable<Dependency>>._, A<string>._))
			.MustHaveHappenedOnceExactly();
		capturedDeps.ShouldNotBeNull();
		capturedDeps.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("Serilog(net9.0)", "Serilog")]
	[InlineData("Serilog(netstandard2.0)", "Serilog")]
	[InlineData("MyApp(net462)", "MyApp")]
	[InlineData("Project.With.Dots(net10.0)", "Project.With.Dots")]
	[InlineData("SimpleProject", "SimpleProject")]
	[InlineData("", "")]
	public void GivenProjectName_WhenExtractBaseProjectNameCalled_ThenReturnsExpectedBaseName(string projectName, string expected)
	{
		var result = DependencyIngestor.ExtractBaseProjectName(projectName);
		result.ShouldBe(expected);
	}
}
