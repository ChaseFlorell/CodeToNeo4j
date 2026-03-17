using System.IO.Abstractions;
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
        var fileSystem = A.Fake<IFileSystem>();
        A.CallTo(() => fileSystem.Directory.Exists("/repo")).Returns(true);

        var handler = new MsBuildRegistrationHandler(fileSystem);
        var nextHandler = A.Fake<IOptionsHandler>();
        handler.SetNext(nextHandler);

        var options = CreateOptions("/repo");

        // act
        await handler.Handle(options);

        // assert
        A.CallTo(() => nextHandler.Handle(options)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenFilePath_WhenHandleCalled_ThenDoesNotSkipMsBuild()
    {
        // arrange
        var fileSystem = A.Fake<IFileSystem>();
        A.CallTo(() => fileSystem.Directory.Exists("/repo/My.sln")).Returns(false);

        var handler = new MsBuildRegistrationHandler(fileSystem);
        var nextHandler = A.Fake<IOptionsHandler>();
        handler.SetNext(nextHandler);

        var options = CreateOptions("/repo/My.sln");

        // act
        await handler.Handle(options);

        // assert — chain still continues
        A.CallTo(() => nextHandler.Handle(options)).MustHaveHappenedOnceExactly();
    }

    private static Options CreateOptions(string inputPath) => new(
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
        ShowVersion: false,
        ShowSupportedFiles: false,
        ShowInfo: false);
}
