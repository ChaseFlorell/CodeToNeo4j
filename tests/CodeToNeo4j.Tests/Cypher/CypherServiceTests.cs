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
		CypherService sut = new();

		// Act
		var result = sut.GetCypher(queryName);

		// Assert
		result.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void GivenUnknownQueryName_WhenGetCypherCalled_ThenThrowsFileNotFoundException()
	{
		// Arrange
		CypherService sut = new();

		// Act & Assert
		Should.Throw<FileNotFoundException>(() => sut.GetCypher("NonExistentQuery"));
	}

	[Fact]
	public void GivenSameQueryName_WhenGetCypherCalledTwice_ThenReturnsSameContent()
	{
		// Arrange
		CypherService sut = new();

		// Act
		var first = sut.GetCypher(Queries.Schema);
		var second = sut.GetCypher(Queries.Schema);

		// Assert
		first.ShouldBe(second);
	}

	[Theory]
	[InlineData("CREATE TEXT INDEX src__symbol_documentation IF NOT EXISTS")]
	[InlineData("CREATE TEXT INDEX src__symbol_comments IF NOT EXISTS")]
	[InlineData("DROP INDEX symbol_documentation IF EXISTS")]
	[InlineData("DROP INDEX symbol_comments IF EXISTS")]
	public void GivenSchema_WhenGetCypherCalled_ThenContainsTextIndexStatements(string expected)
	{
		// Arrange
		CypherService sut = new();

		// Act
		var schema = sut.GetCypher(Queries.Schema);

		// Assert
		schema.ShouldContain(expected);
	}

	[Theory]
	[InlineData("CREATE INDEX symbol_documentation IF NOT EXISTS")]
	[InlineData("CREATE INDEX symbol_comments IF NOT EXISTS")]
	public void GivenSchema_WhenGetCypherCalled_ThenDoesNotContainLegacyRangeIndexes(string legacy)
	{
		// Arrange
		CypherService sut = new();

		// Act
		var schema = sut.GetCypher(Queries.Schema);

		// Assert
		schema.ShouldNotContain(legacy);
	}
}
