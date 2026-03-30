using CodeToNeo4j.Cypher;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.Graph.Xml;
using CodeToNeo4j.Neo4j;
using CodeToNeo4j.VersionControl;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Xunit;

namespace CodeToNeo4j.Tests.Neo4j;

public class Neo4jFlushServiceTests
{
	private static (Neo4jFlushService sut, IDriver driver) CreateSut()
	{
		var driver = A.Fake<IDriver>();
		var cypherService = A.Fake<ICypherService>();
		var namespaceTagParser = A.Fake<INamespaceTagParser>();
		var logger = A.Fake<ILogger<Neo4jFlushService>>();
		Neo4jFlushService sut = new(driver, cypherService, namespaceTagParser, logger);
		return (sut, driver);
	}

	private static IAsyncSession SetupSession(IDriver driver)
	{
		var session = A.Fake<IAsyncSession>();
		A.CallTo(() => driver.AsyncSession()).Returns(session);
		A.CallTo(() => driver.AsyncSession(A<Action<SessionConfigBuilder>>._)).Returns(session);
		return session;
	}

	[Fact]
	public async Task GivenEmptyFiles_WhenFlushFilesCalled_ThenDoesNotOpenSession()
	{
		// Arrange
		var (sut, driver) = CreateSut();

		// Act
		await sut.FlushFiles([], "testdb");

		// Assert
		A.CallTo(() => driver.AsyncSession()).MustNotHaveHappened();
		A.CallTo(() => driver.AsyncSession(A<Action<SessionConfigBuilder>>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenFiles_WhenFlushFilesCalled_ThenExecutesWrite()
	{
		// Arrange
		var (sut, driver) = CreateSut();
		FileMetadata metadata = new(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
		var files = new[] { new FileMetaData("key", "file.cs", "file.cs", "hash", metadata, "repo", "ns") };
		var session = SetupSession(driver);

		// Act
		await sut.FlushFiles(files, "testdb");

		// Assert
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._, A<Action<TransactionConfigBuilder>?>._)).MustHaveHappened();
	}

	[Fact]
	public async Task GivenEmptySymbolsAndRels_WhenFlushSymbolsCalled_ThenDoesNotOpenSession()
	{
		// Arrange
		var (sut, driver) = CreateSut();

		// Act
		await sut.FlushSymbols([], [], "testdb");

		// Assert
		A.CallTo(() => driver.AsyncSession()).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenSymbolsButNoRels_WhenFlushSymbolsCalled_ThenExecutesWrite()
	{
		// Arrange
		var (sut, driver) = CreateSut();
		var symbols = new[] { new Symbol("k1", "Foo", "NamedType", "class", "Foo", "Public", "f1", "f1.cs", 1, 10, null, null, "ns") };
		var session = SetupSession(driver);

		// Act
		await sut.FlushSymbols(symbols, [], "testdb");

		// Assert
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._, A<Action<TransactionConfigBuilder>?>._)).MustHaveHappened();
	}

	[Fact]
	public async Task GivenRelsButNoSymbols_WhenFlushSymbolsCalled_ThenExecutesWrite()
	{
		// Arrange
		var (sut, driver) = CreateSut();
		var rels = new[] { new Relationship("k1", "k2", GraphSchema.Relationships.DependsOn) };
		var session = SetupSession(driver);

		// Act
		await sut.FlushSymbols([], rels, "testdb");

		// Assert
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._, A<Action<TransactionConfigBuilder>?>._)).MustHaveHappened();
	}

	[Fact]
	public async Task GivenSymbolsWithNamespace_WhenFlushSymbolsCalled_ThenUpsertsTags()
	{
		// Arrange
		var driver = A.Fake<IDriver>();
		var cypherService = A.Fake<ICypherService>();
		var namespaceTagParser = A.Fake<INamespaceTagParser>();
		string[] tags = ["My", "Namespace"];
		A.CallTo(() => namespaceTagParser.ParseTags("My.Namespace")).Returns(tags);
		var logger = A.Fake<ILogger<Neo4jFlushService>>();
		Neo4jFlushService sut = new(driver, cypherService, namespaceTagParser, logger);
		var symbols = new[] { new Symbol("k1", "Foo", "NamedType", "class", "Foo", "Public", "f1", "f1.cs", 1, 10, null, null, "My.Namespace") };
		var session = SetupSession(driver);

		// Act
		await sut.FlushSymbols(symbols, [], "testdb");

		// Assert
		// 1 for symbols/rels, 1 for tags
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._, A<Action<TransactionConfigBuilder>?>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task GivenUrls_WhenUpsertDependencyUrlsCalled_ThenExecutesWrite()
	{
		// Arrange
		var (sut, driver) = CreateSut();
		var urls = new[] { new UrlNode("dep", "url", "name") };
		var session = SetupSession(driver);

		// Act
		await sut.UpsertDependencyUrls(urls, "testdb");

		// Assert
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task<IResultCursor>>>._, A<Action<TransactionConfigBuilder>?>._))
			.MustHaveHappened();
	}
}
