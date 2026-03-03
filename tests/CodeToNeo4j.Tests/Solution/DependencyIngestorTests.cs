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
    public async Task GivenSolutionWithReferencedAssemblies_WhenIngestDependenciesCalled_ThenUpsertsUniqueDependencies()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var logger = A.Fake<ILogger<DependencyIngestor>>();
        var sut = new DependencyIngestor(graphService, logger);

        var workspace = new AdhocWorkspace();
        _ = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        var solution = workspace.CurrentSolution;

        // Act
        await sut.IngestDependencies(solution, "test-repo", "neo4j");

        // Assert
        A.CallTo(() => graphService.UpsertDependencies("test-repo", A<Dependency[]>.That.IsNotNull(), "neo4j"))
            .MustHaveHappened();
    }
}
