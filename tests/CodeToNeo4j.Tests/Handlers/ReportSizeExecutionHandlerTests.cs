using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Models;
using CodeToNeo4j.ProgramOptions;
using CodeToNeo4j.ProgramOptions.Handlers;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeToNeo4j.Tests.Handlers;

public class ReportSizeExecutionHandlerTests
{
	[Fact]
	public async Task GivenReportSize_WhenHandleCalled_ThenDelegatesToGraphServiceAndStopsChain()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		A.CallTo(() => graphService.GetProjectSizeReport("neo4j"))
			.Returns(Task.FromResult<IReadOnlyList<ProjectSizeReport>>([]));
		ReportSizeExecutionHandler handler = new(graphService);

		var next = A.Fake<IOptionsHandler>();
		handler.SetNext(next);

		var options = CreateOptions(reportSize: true);

		// Act
		await handler.Handle(options);

		// Assert
		A.CallTo(() => graphService.GetProjectSizeReport("neo4j")).MustHaveHappenedOnceExactly();
		A.CallTo(() => next.Handle(A<Options>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenReportSizeNotRequested_WhenHandleCalled_ThenContinuesChainWithoutCallingGraphService()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		ReportSizeExecutionHandler handler = new(graphService);

		var next = A.Fake<IOptionsHandler>();
		handler.SetNext(next);

		var options = CreateOptions(reportSize: false);

		// Act
		await handler.Handle(options);

		// Assert
		A.CallTo(() => graphService.GetProjectSizeReport(A<string>._)).MustNotHaveHappened();
		A.CallTo(() => next.Handle(options)).MustHaveHappenedOnceExactly();
	}

	private static Options CreateOptions(bool reportSize)
	{
		MockFileSystem fs = new();
		return new(
			fs.FileInfo.New("test.sln"),
			"my-repo",
			"bolt://localhost",
			"user",
			"pass",
			false,
			null,
			100,
			"neo4j",
			Microsoft.Extensions.Logging.LogLevel.Information,
			false,
			Accessibility.Private,
			[],
			false,
			reportSize,
			false,
			false,
			false);
	}
}
