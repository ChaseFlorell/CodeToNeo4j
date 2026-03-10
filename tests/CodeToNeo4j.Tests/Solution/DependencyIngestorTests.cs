using CodeToNeo4j.Graph;
using CodeToNeo4j.Solution;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Solution;

public class DependencyIngestorTests
{
    [Fact]
    public async Task GivenSolutionWithProject_WhenIngestDependenciesCalled_ThenKeysAreVersionless()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var logger = A.Fake<ILogger<DependencyIngestor>>();
        var sut = new DependencyIngestor(graphService, logger);

        var workspace = new AdhocWorkspace();
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
}
