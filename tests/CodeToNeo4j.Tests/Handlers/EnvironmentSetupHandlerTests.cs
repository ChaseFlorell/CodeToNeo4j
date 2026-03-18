using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Graph;
using CodeToNeo4j.ProgramOptions;
using CodeToNeo4j.ProgramOptions.Handlers;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Xunit;

namespace CodeToNeo4j.Tests.Handlers;

public class EnvironmentSetupHandlerTests
{
    [Fact]
    public async Task GivenOptions_WhenHandleCalled_ThenDelegatesToGraphServiceInitialize()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var handler = new EnvironmentSetupHandler(graphService);

        var fs = new MockFileSystem();
        var options = new Options(
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
            ShowVersion: false,
            ShowSupportedFiles: false,
            ShowInfo: false);

        // Act
        await handler.Handle(options);

        // Assert
        A.CallTo(() => graphService.Initialize("my-repo", "neo4j")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenNullRepoKey_WhenHandleCalled_ThenDelegatesToGraphServiceWithNullKey()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var handler = new EnvironmentSetupHandler(graphService);

        var fs = new MockFileSystem();
        var options = new Options(
            fs.FileInfo.New("test.sln"),
            null,
            "bolt://localhost",
            "user",
            "pass",
            true,
            null,
            100,
            "neo4j",
            Microsoft.Extensions.Logging.LogLevel.Information,
            false,
            Accessibility.Private,
            [],
            false,
            ShowVersion: false,
            ShowSupportedFiles: false,
            ShowInfo: false);

        // Act
        await handler.Handle(options);

        // Assert
        A.CallTo(() => graphService.Initialize(null, "neo4j")).MustHaveHappenedOnceExactly();
    }
}
