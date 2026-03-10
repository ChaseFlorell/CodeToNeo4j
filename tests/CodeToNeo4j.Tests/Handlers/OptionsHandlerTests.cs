using Xunit;
using FakeItEasy;
using CodeToNeo4j.ProgramOptions;
using CodeToNeo4j.ProgramOptions.Handlers;

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

        // act
        await handler.Handle(options);

        // assert
        A.CallTo(() => nextHandler.Handle(options)).MustHaveHappenedOnceExactly();
    }

    private class TestHandler : OptionsHandler
    {
        protected override Task<bool> HandleOptions(Options options) => Task.FromResult(true);
    }

    [Fact]
    public async Task GivenPurgeDataTrue_WhenPurgeExecutionHandlerCalled_ThenPurgeDataIsCalledAndChainStops()
    {
        // arrange

        var graphService = A.Fake<CodeToNeo4j.Graph.IGraphService>();
        var handler = new PurgeExecutionHandler(graphService);
        var nextHandler = A.Fake<IOptionsHandler>();
        handler.SetNext(nextHandler);

        var options = CreateOptions(purgeData: true);

        var serviceProvider = A.Fake<IServiceProvider>();
        A.CallTo(() => serviceProvider.GetService(typeof(CodeToNeo4j.Graph.IGraphService))).Returns(graphService);


        // act
        await handler.Handle(options);

        // assert
        A.CallTo(() => graphService.PurgeData(A<string>._, A<IEnumerable<string>>._, A<string>._, A<bool>._, A<int>._)).MustHaveHappenedOnceExactly();
        A.CallTo(() => nextHandler.Handle(A<Options>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenPurgeDataFalse_WhenPurgeExecutionHandlerCalled_ThenPurgeDataIsNotCalledAndChainContinues()
    {
        // arrange
        var graphService = A.Fake<CodeToNeo4j.Graph.IGraphService>();
        var handler = new PurgeExecutionHandler(graphService);
        var nextHandler = A.Fake<IOptionsHandler>();
        handler.SetNext(nextHandler);

        var options = CreateOptions(purgeData: false);

        // act
        await handler.Handle(options);

        // assert
        A.CallTo(() => nextHandler.Handle(options)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GivenHandlers_WhenBuildChainCalled_ThenHandlersAreChainedCorrectly()
    {
        // arrange
        var h1 = A.Fake<IOptionsHandler>();
        var h2 = A.Fake<IOptionsHandler>();
        var h3 = A.Fake<IOptionsHandler>();
        var handlers = new[] { h1, h2, h3 };

        // act
        var result = handlers.BuildChain();

        // assert
        Assert.Same(h1, result);
        A.CallTo(() => h1.SetNext(h2)).MustHaveHappenedOnceExactly();
        A.CallTo(() => h2.SetNext(h3)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GivenOptions_WhenToStringCalled_ThenIncludeExtensionsAreFormattedCorrectly()
    {
        // arrange
        var options = CreateOptions(includeExtensions: [".cs", ".xml"]);

        // act
        var result = options.ToString();

        // assert
        Assert.Contains("IncludeExtensions = [ .cs, .xml ]", result);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task GivenPurgeDataTrue_WhenPurgeExecutionHandlerCalled_ThenPurgeDataIsCalledWithCorrectPurgeDependencies(bool skipDependencies, bool expectedPurgeDependencies)
    {
        // arrange
        var graphService = A.Fake<CodeToNeo4j.Graph.IGraphService>();
        var handler = new PurgeExecutionHandler(graphService);
        var options = CreateOptions(purgeData: true, skipDependencies: skipDependencies);

        // act
        await handler.Handle(options);

        // assert
        A.CallTo(() => graphService.PurgeData(A<string>._, A<IEnumerable<string>>._, A<string>._, expectedPurgeDependencies, A<int>._)).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenPurgeDataTrueAndDefaultExtensions_WhenPurgeExecutionHandlerCalled_ThenPurgeDataIsCalledWithNullExtensions()
    {
        // arrange
        var graphService = A.Fake<CodeToNeo4j.Graph.IGraphService>();
        var handler = new PurgeExecutionHandler(graphService);
        var allSupportedExtensions = new[] { ".cs", ".razor", ".xaml", ".js", ".html", ".xml", ".json", ".css", ".csproj" };
        var options = CreateOptions(purgeData: true, includeExtensions: allSupportedExtensions);

        // act
        await handler.Handle(options);

        // assert
        A.CallTo(() => graphService.PurgeData(A<string>._, null, A<string>._, A<bool>._, A<int>._)).MustHaveHappenedOnceExactly();
    }

    private static Options CreateOptions(bool purgeData = false,
        bool skipDependencies = false,
        string[]? includeExtensions = null) => new(
        new FileInfo("test.sln"),
        "bolt://localhost",
        "user",
        "pass",
        false,
        null,
        100,
        "neo4j",
        Microsoft.Extensions.Logging.LogLevel.Information,
        skipDependencies,
        Microsoft.CodeAnalysis.Accessibility.Private,
        includeExtensions ?? [],
        purgeData);
}