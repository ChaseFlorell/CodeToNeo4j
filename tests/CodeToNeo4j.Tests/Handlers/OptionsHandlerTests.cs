using Xunit;
using FakeItEasy;
using CodeToNeo4j.ProgramOptions;

namespace CodeToNeo4j.Tests.Handlers;

public class OptionsHandlerTests
{
    [Fact]
    public async Task GivenNextHandler_WhenBaseHandleCalled_ThenNextHandlerIsCalled()
    {
        // arrange
        var handler = new TestHandler();
        var nextHandler = A.Fake<IOptionsHandler>();
        handler.SetNext(nextHandler);
        var options = CreateOptions();
        var context = new HandlerContext();

        // act
        await handler.Handle(options, context);

        // assert
        A.CallTo(() => nextHandler.Handle(options, context)).MustHaveHappenedOnceExactly();
    }

    private class TestHandler : OptionsHandler { }

    [Fact]
    public async Task GivenPurgeDataTrue_WhenPurgeExecutionHandlerCalled_ThenPurgeDataIsCalledAndChainTerminates()
    {
        // arrange
        string[] allSupportedExtensions = [".cs"];
        var handler = new PurgeExecutionHandler(allSupportedExtensions);
        var nextHandler = A.Fake<IOptionsHandler>();
        handler.SetNext(nextHandler);
        
        var options = CreateOptions(purgeData: true);
        
        var serviceProvider = A.Fake<IServiceProvider>();
        var graphService = A.Fake<CodeToNeo4j.Graph.IGraphService>();
        A.CallTo(() => serviceProvider.GetService(typeof(CodeToNeo4j.Graph.IGraphService))).Returns(graphService);
        
        var context = new HandlerContext(serviceProvider);

        // act
        await handler.Handle(options, context);

        // assert
        A.CallTo(() => graphService.PurgeData(A<string>._, A<IEnumerable<string>>._, A<string>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => nextHandler.Handle(A<Options>._, A<HandlerContext>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenPurgeDataFalse_WhenPurgeExecutionHandlerCalled_ThenPurgeDataIsNotCalledAndChainContinues()
    {
        // arrange
        string[] allSupportedExtensions = [".cs"];
        var handler = new PurgeExecutionHandler(allSupportedExtensions);
        var nextHandler = A.Fake<IOptionsHandler>();
        handler.SetNext(nextHandler);
        
        var options = CreateOptions(purgeData: false);
        var context = new HandlerContext();

        // act
        await handler.Handle(options, context);

        // assert
        A.CallTo(() => nextHandler.Handle(options, context)).MustHaveHappenedOnceExactly();
    }

    private static Options CreateOptions(bool purgeData = false) => new(
        new FileInfo("test.sln"),
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
        purgeData);
}
