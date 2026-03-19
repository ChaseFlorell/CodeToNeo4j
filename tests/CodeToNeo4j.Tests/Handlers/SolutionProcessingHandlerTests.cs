using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.ProgramOptions;
using CodeToNeo4j.ProgramOptions.Handlers;
using CodeToNeo4j.Solution;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeToNeo4j.Tests.Handlers;

public class SolutionProcessingHandlerTests
{
	[Fact]
	public async Task GivenOptions_WhenHandleCalled_ThenDelegatesToSolutionProcessor()
	{
		// arrange
		var solutionProcessor = A.Fake<ISolutionProcessor>();
		SolutionProcessingHandler handler = new(solutionProcessor);

		MockFileSystem fs = new();
		Options options = new(
			fs.FileInfo.New("/repo/My.sln"),
			"my",
			"bolt://localhost",
			"user",
			"pass",
			false,
			"origin/main",
			500,
			"neo4j",
			Microsoft.Extensions.Logging.LogLevel.Information,
			false,
			Accessibility.Public,
			[".cs"],
			false,
			false,
			false,
			false);

		// act
		await handler.Handle(options);

		// assert
		A.CallTo(() => solutionProcessor.ProcessSolution(
			"/repo/My.sln",
			"my",
			"origin/main",
			"neo4j",
			500,
			false,
			Accessibility.Public,
			A<IEnumerable<string>>._)).MustHaveHappenedOnceExactly();
	}
}
