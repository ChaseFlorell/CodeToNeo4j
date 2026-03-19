using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.ProgramOptions;
using CodeToNeo4j.ProgramOptions.Handlers;
using FakeItEasy;
using Xunit;

namespace CodeToNeo4j.Tests.Handlers;

public class MsBuildRegistrationHandlerTests
{
	[Fact]
	public async Task GivenDirectoryPath_WhenHandleCalled_ThenSkipsMsBuildRegistrationAndContinuesChain()
	{
		// arrange
		MsBuildRegistrationHandler handler = new();
		var nextHandler = A.Fake<IOptionsHandler>();
		handler.SetNext(nextHandler);

		MockFileSystem fs = new();
		var options = CreateOptions(fs.DirectoryInfo.New("/repo"));

		// act
		await handler.Handle(options);

		// assert
		A.CallTo(() => nextHandler.Handle(options)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenFilePath_WhenHandleCalled_ThenDoesNotSkipMsBuild()
	{
		// arrange
		MsBuildRegistrationHandler handler = new();
		var nextHandler = A.Fake<IOptionsHandler>();
		handler.SetNext(nextHandler);

		MockFileSystem fs = new();
		var options = CreateOptions(fs.FileInfo.New("/repo/My.sln"));

		// act
		await handler.Handle(options);

		// assert — chain still continues
		A.CallTo(() => nextHandler.Handle(options)).MustHaveHappenedOnceExactly();
	}

	private static Options CreateOptions(IFileSystemInfo inputPath) => new(
		inputPath,
		"test",
		"bolt://localhost",
		"user",
		"pass",
		false,
		null,
		100,
		"neo4j",
		Microsoft.Extensions.Logging.LogLevel.Information,
		false,
		Microsoft.CodeAnalysis.Accessibility.Private,
		[],
		false,
		false,
		false,
		false);
}
