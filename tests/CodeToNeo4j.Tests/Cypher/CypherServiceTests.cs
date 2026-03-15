using CodeToNeo4j.Cypher;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Cypher;

public class CypherServiceTests
{
    [Theory]
    [InlineData(Queries.Schema)]
    [InlineData(Queries.UpsertFile)]
    [InlineData(Queries.UpsertSymbols)]
    [InlineData(Queries.MergeRelationships)]
    [InlineData(Queries.UpsertProject)]
    [InlineData(Queries.MarkFileAsDeleted)]
    [InlineData(Queries.PurgeData)]
    public void GivenKnownQueryName_WhenGetCypherCalled_ThenReturnsNonEmptyContent(string queryName)
    {
        // Arrange
        var sut = new CypherService();

        // Act
        var result = sut.GetCypher(queryName);

        // Assert
        result.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GivenUnknownQueryName_WhenGetCypherCalled_ThenThrowsFileNotFoundException()
    {
        // Arrange
        var sut = new CypherService();

        // Act & Assert
        Should.Throw<FileNotFoundException>(() => sut.GetCypher("NonExistentQuery"));
    }

    [Fact]
    public void GivenSameQueryName_WhenGetCypherCalledTwice_ThenReturnsSameContent()
    {
        // Arrange
        var sut = new CypherService();

        // Act
        var first = sut.GetCypher(Queries.Schema);
        var second = sut.GetCypher(Queries.Schema);

        // Assert
        first.ShouldBe(second);
    }
}
