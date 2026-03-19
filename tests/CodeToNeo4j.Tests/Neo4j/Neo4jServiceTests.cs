using CodeToNeo4j.Cypher;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Neo4j;
using CodeToNeo4j.VersionControl;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Neo4j.Driver;
using Xunit;

namespace CodeToNeo4j.Tests.Neo4j;

public class Neo4jServiceTests
{
	[Fact]
	public async Task GivenEmptyCommitBatch_WhenUpsertCommitsCalled_ThenDoesNotCreateSession()
	{
		// Arrange
		var driver = A.Fake<IDriver>();
		var sut = CreateService(driver);

		// Act
		await sut.UpsertCommits("repo", "/root", [], "testdb");

		// Assert
		A.CallTo(() => driver.AsyncSession(A<Action<SessionConfigBuilder>>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenEmptyDependencies_WhenUpsertDependenciesCalled_ThenDoesNotCreateSession()
	{
		// Arrange
		var driver = A.Fake<IDriver>();
		var sut = CreateService(driver);

		// Act
		await sut.UpsertDependencies("repo", [], "testdb");

		// Assert
		A.CallTo(() => driver.AsyncSession(A<Action<SessionConfigBuilder>>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenFlushFiles_WhenCalled_ThenDelegatesToFlushService()
	{
		// Arrange
		var flushService = A.Fake<INeo4jFlushService>();
		var sut = CreateService(flushService: flushService);
		FileMetadata metadata = new(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
		var files = new[] { new FileMetaData("key1", "file.cs", "file.cs", "hash", metadata, "repo", "ns") };

		// Act
		await sut.FlushFiles(files, "testdb");

		// Assert
		A.CallTo(() => flushService.FlushFiles(A<IEnumerable<FileMetaData>>._, "testdb")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenFlushSymbols_WhenCalled_ThenDelegatesToFlushService()
	{
		// Arrange
		var flushService = A.Fake<INeo4jFlushService>();
		var sut = CreateService(flushService: flushService);
		var symbols = new[] { new Symbol("k1", "Foo", "NamedType", "class", "Foo", "Public", "key", "file.cs", 1, 10, null, null, "ns") };
		var rels = new[] { new Relationship("k1", "k2", "CONTAINS") };

		// Act
		await sut.FlushSymbols(symbols, rels, "testdb");

		// Assert
		A.CallTo(() => flushService.FlushSymbols(A<IEnumerable<Symbol>>._, A<IEnumerable<Relationship>>._, "testdb")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenUpsertDependencyUrls_WhenCalled_ThenDelegatesToFlushService()
	{
		// Arrange
		var flushService = A.Fake<INeo4jFlushService>();
		var sut = CreateService(flushService: flushService);
		var urls = new[] { new UrlNode("dep:pkg", "https://example.com", "example") };

		// Act
		await sut.UpsertDependencyUrls(urls, "testdb");

		// Assert
		A.CallTo(() => flushService.UpsertDependencyUrls(A<IEnumerable<UrlNode>>._, "testdb")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenInitialize_WhenCalled_ThenDelegatesToSchemaService()
	{
		// Arrange
		var schemaService = A.Fake<INeo4jSchemaService>();
		var sut = CreateService(schemaService: schemaService);

		// Act
		await sut.Initialize("repo", "testdb");

		// Assert
		A.CallTo(() => schemaService.Initialize("repo", "testdb")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenMarkFileAsDeleted_WhenCalled_ThenExecutesDeleteQuery()
	{
		// Arrange
		var driver = A.Fake<IDriver>();
		var session = A.Fake<IAsyncSession>();
		var cypherService = A.Fake<ICypherService>();
		A.CallTo(() => driver.AsyncSession(A<Action<SessionConfigBuilder>>._)).Returns(session);
		A.CallTo(() => cypherService.GetCypher(Queries.MarkFileAsDeleted)).Returns("MATCH (f) SET f.deleted = true");
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._))
			.Returns(Task.CompletedTask);

		var sut = CreateService(driver, cypherService);

		// Act
		await sut.MarkFileAsDeleted("some/file.cs", "testdb");

		// Assert
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenNonEmptyCommits_WhenUpsertCommitsCalled_ThenCreatesSession()
	{
		// Arrange
		var driver = A.Fake<IDriver>();
		var session = A.Fake<IAsyncSession>();
		var cypherService = A.Fake<ICypherService>();
		var fileService = A.Fake<IFileService>();
		A.CallTo(() => driver.AsyncSession(A<Action<SessionConfigBuilder>>._)).Returns(session);
		A.CallTo(() => cypherService.GetCypher(Queries.UpsertCommit)).Returns("MERGE (c:Commit)");
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => fileService.GetRelativePath(A<string>._, A<string>._)).ReturnsLazily((string root, string path) => path);
		A.CallTo(() => fileService.InferFileMetadata(A<string>._)).Returns(("key", "ns"));

		var sut = CreateService(driver, cypherService, fileService);
		var commits = new[]
		{
			new CommitMetadata("abc", "Author", "a@b.com", DateTimeOffset.Now, "msg",
				[new("/repo/file.cs", false)])
		};

		// Act
		await sut.UpsertCommits("repo", "/root", commits, "testdb");

		// Assert
		A.CallTo(() => driver.AsyncSession(A<Action<SessionConfigBuilder>>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => session.ExecuteWriteAsync(A<Func<IAsyncQueryRunner, Task>>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void GivenDispose_WhenCalled_ThenDisposesDriver()
	{
		// Arrange
		var driver = A.Fake<IDriver>();
		var sut = CreateService(driver);

		// Act
		sut.Dispose();

		// Assert
		A.CallTo(() => driver.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenDisposeAsync_WhenCalled_ThenDisposesDriverAsync()
	{
		// Arrange
		var driver = A.Fake<IDriver>();
		var sut = CreateService(driver);

		// Act
		await sut.DisposeAsync();

		// Assert
		A.CallTo(() => driver.DisposeAsync()).MustHaveHappened();
	}


	[Fact]
	public async Task GivenFlushTargetFrameworks_WhenCalled_ThenDelegatesToFlushService()
	{
		// Arrange
		var flushService = A.Fake<INeo4jFlushService>();
		var sut = CreateService(flushService: flushService);
		var batches = new[] { new TargetFrameworkBatch("fileKey", ["sym1"], ["net9.0"]) };

		// Act
		await sut.FlushTargetFrameworks(batches, "testdb");

		// Assert
		A.CallTo(() => flushService.FlushTargetFrameworks(A<IEnumerable<TargetFrameworkBatch>>._, "testdb")).MustHaveHappenedOnceExactly();
	}

	private static Neo4jService CreateService(
		IDriver? driver = null,
		ICypherService? cypherService = null,
		IFileService? fileService = null,
		INeo4jSchemaService? schemaService = null,
		INeo4jFlushService? flushService = null)
	{
		return new(
			driver ?? A.Fake<IDriver>(),
			cypherService ?? A.Fake<ICypherService>(),
			fileService ?? A.Fake<IFileService>(),
			schemaService ?? A.Fake<INeo4jSchemaService>(),
			flushService ?? A.Fake<INeo4jFlushService>(),
			A.Fake<ILogger<Neo4jService>>());
	}
}
