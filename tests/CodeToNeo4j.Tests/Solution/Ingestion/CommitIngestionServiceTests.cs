#pragma warning disable CS8600, CS8604 // FakeItEasy argument matchers produce null for non-nullable types
using CodeToNeo4j.Graph;
using CodeToNeo4j.Solution.Ingestion;
using CodeToNeo4j.VersionControl;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeToNeo4j.Tests.Solution.Ingestion;

public class CommitIngestionServiceTests
{
	[Fact]
	public async Task GivenZeroCommits_WhenIngestCommitsCalled_ThenDoesNotCallUpsert()
	{
		// Arrange
		var vcs = A.Fake<IVersionControlService>();
		var graph = A.Fake<IGraphService>();
		var logger = A.Fake<ILogger<CommitIngestionService>>();
		A.CallTo(() => vcs.GetCommitCount("main", "/repo")).Returns(0);
		CommitIngestionService sut = new(vcs, graph, logger);

		// Act
		await sut.IngestCommits("main", "/repo", "key", "db", 100);

		// Assert
		A.CallTo(() => graph.UpsertCommits(A<string>._, A<string>._, A<IEnumerable<CommitMetadata>>._, A<string>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenCommitsExist_WhenIngestCommitsCalled_ThenCallsGetCommitBatchAndUpsert()
	{
		// Arrange
		var vcs = A.Fake<IVersionControlService>();
		var graph = A.Fake<IGraphService>();
		var logger = A.Fake<ILogger<CommitIngestionService>>();
		A.CallTo(() => vcs.GetCommitCount("main", "/repo")).Returns(5);
		A.CallTo(() => vcs.GetCommitBatch("main", "/repo", 100, 0))
			.Returns(new List<CommitMetadata> { new("h1", "Author", "a@b.com", DateTimeOffset.Now, "msg", []) });
		CommitIngestionService sut = new(vcs, graph, logger);

		// Act
		await sut.IngestCommits("main", "/repo", "key", "db", 100);

		// Assert
		A.CallTo(() => vcs.GetCommitBatch("main", "/repo", 100, 0)).MustHaveHappenedOnceExactly();
		A.CallTo(() => graph.UpsertCommits("key", "/repo", A<IEnumerable<CommitMetadata>>._, "db"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenMoreCommitsThanBatchSize_WhenIngestCommitsCalled_ThenCallsMultipleBatches()
	{
		// Arrange
		var vcs = A.Fake<IVersionControlService>();
		var graph = A.Fake<IGraphService>();
		var logger = A.Fake<ILogger<CommitIngestionService>>();
		A.CallTo(() => vcs.GetCommitCount("main", "/repo")).Returns(250);
		A.CallTo(() => vcs.GetCommitBatch(A<string>._, A<string>._, A<int>._, A<int>._))
			.Returns(new List<CommitMetadata>());
		CommitIngestionService sut = new(vcs, graph, logger);

		// Act
		await sut.IngestCommits("main", "/repo", "key", "db", 100);

		// Assert — 250 commits / batch 100 = 3 batches (0, 100, 200)
		A.CallTo(() => vcs.GetCommitBatch("main", "/repo", 100, A<int>._))
			.MustHaveHappened(3, Times.Exactly);
	}
}
#pragma warning restore CS8600, CS8604
