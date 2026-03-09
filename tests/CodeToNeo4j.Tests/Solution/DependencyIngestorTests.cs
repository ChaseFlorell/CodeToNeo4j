using CodeToNeo4j.Graph;
using CodeToNeo4j.Solution;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeToNeo4j.Tests.Solution;

public class DependencyIngestorTests
{
    [Fact]
    public async Task GivenSolutionWithMultipleProjectsAndOverlappingDependencies_WhenIngestDependenciesCalled_ThenUpsertsUniqueDependencies()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var logger = A.Fake<ILogger<DependencyIngestor>>();
        var sut = new DependencyIngestor(graphService, logger);

        var workspace = new AdhocWorkspace();
        var commonRef = MetadataReference.CreateFromFile(typeof(object).Assembly.Location);
        
        var project1 = workspace.AddProject("Project1", LanguageNames.CSharp);
        workspace.TryApplyChanges(workspace.CurrentSolution.AddMetadataReference(project1.Id, commonRef));
        
        var project2 = workspace.AddProject("Project2", LanguageNames.CSharp);
        workspace.TryApplyChanges(workspace.CurrentSolution.AddMetadataReference(project2.Id, commonRef));
            
        var solution = workspace.CurrentSolution;

        // Act
        await sut.IngestDependencies(solution, "test-repo", "neo4j");

        // Assert
        A.CallTo(() => graphService.UpsertDependencies("test-repo", A<Dependency[]>.That.Matches(d => d.Length == 1), "neo4j"))
            .MustHaveHappened();
    }
}
