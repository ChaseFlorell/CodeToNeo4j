using System.IO.Abstractions;
using System.Threading.Channels;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Progress;
using CodeToNeo4j.Solution;
using CodeToNeo4j.VersionControl;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Solution;

public class SolutionProcessorTests
{
    [Fact]
    public void GivenNullChangedFiles_WhenFilterFilesCalled_ThenReturnsAllFiles()
    {
        // Arrange
        var files = new[]
        {
            new ProcessedFile("file1.cs"),
            new ProcessedFile("file2.cs"),
            new ProcessedFile("file3.cs")
        };

        // Act
        var result = SolutionProcessor.FilterFiles(files, null);

        // Assert
        result.Length.ShouldBe(3);
    }

    [Fact]
    public void GivenEmptyChangedFiles_WhenFilterFilesCalled_ThenReturnsEmptyArray()
    {
        // Arrange
        var files = new[]
        {
            new ProcessedFile("file1.cs"),
            new ProcessedFile("file2.cs")
        };
        var changedFiles = new HashSet<string>();

        // Act
        var result = SolutionProcessor.FilterFiles(files, changedFiles);

        // Assert
        result.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("file1.cs", 1)]
    [InlineData("file2.cs", 1)]
    public void GivenChangedFilesSet_WhenFilterFilesCalled_ThenReturnsOnlyMatchingFiles(string changedFile, int expectedCount)
    {
        // Arrange
        var files = new[]
        {
            new ProcessedFile("file1.cs"),
            new ProcessedFile("file2.cs"),
            new ProcessedFile("file3.cs")
        };
        var changedFiles = new HashSet<string> { changedFile };

        // Act
        var result = SolutionProcessor.FilterFiles(files, changedFiles);

        // Assert
        result.Length.ShouldBe(expectedCount);
        result[0].FilePath.ShouldBe(changedFile);
    }

    [Fact]
    public async Task GivenSingleResult_WhenRunConsumerCalled_ThenFlushesBuffersAndReturnsTotals()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var progressService = A.Fake<IProgressService>();
        var sut = CreateProcessor(graphService: graphService, progressService: progressService);

        var channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
        var metadata = new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
        var fileMetaData = new FileMetaData("key", "file.cs", "file.cs", "hash", metadata, "repo", "ns");
        var symbols = new List<Symbol>
        {
            new("k1", "Foo", "NamedType", "class", "Foo", "Public", "key", "file.cs", 1, 10, null, null, "ns")
        };
        var rels = new List<Relationship>
        {
            new("k1", "k2", "CONTAINS")
        };

        var result = new SolutionProcessor.ProcessResult(fileMetaData, symbols, rels, [], "file.cs");
        await channel.Writer.WriteAsync(result);
        channel.Writer.Complete();

        // Act
        var (totalSymbols, totalRels) = await sut.RunConsumer(channel.Reader, 1, "testdb", 100);

        // Assert
        totalSymbols.ShouldBe(1);
        totalRels.ShouldBe(1);
        A.CallTo(() => graphService.FlushFiles(A<IEnumerable<FileMetaData>>._, "testdb")).MustHaveHappenedOnceExactly();
        A.CallTo(() => graphService.FlushSymbols(A<IEnumerable<Symbol>>._, A<IEnumerable<Relationship>>._, "testdb")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenBatchSizeReached_WhenRunConsumerCalled_ThenFlushesMultipleTimes()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var sut = CreateProcessor(graphService: graphService);

        var channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
        var metadata = new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);

        // Write 3 results with batchSize=2 — should flush twice (once at threshold, once at end)
        for (var i = 0; i < 3; i++)
        {
            var file = new FileMetaData($"key{i}", $"file{i}.cs", $"file{i}.cs", "hash", metadata, "repo", "ns");
            var symbols = new List<Symbol>
            {
                new($"s{i}", $"Sym{i}", "NamedType", "class", $"Sym{i}", "Public", $"key{i}", $"file{i}.cs", 1, 10, null, null, "ns")
            };
            await channel.Writer.WriteAsync(new SolutionProcessor.ProcessResult(file, symbols, [], [], $"file{i}.cs"));
        }

        channel.Writer.Complete();

        // Act
        var (totalSymbols, _) = await sut.RunConsumer(channel.Reader, 3, "testdb", 2);

        // Assert
        totalSymbols.ShouldBe(3);
        A.CallTo(() => graphService.FlushFiles(A<IEnumerable<FileMetaData>>._, "testdb")).MustHaveHappened(2, Times.Exactly);
        A.CallTo(() => graphService.FlushSymbols(A<IEnumerable<Symbol>>._, A<IEnumerable<Relationship>>._, "testdb")).MustHaveHappened(2, Times.Exactly);
    }

    [Fact]
    public async Task GivenEmptyChannel_WhenRunConsumerCalled_ThenReturnsZeroTotals()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var sut = CreateProcessor(graphService: graphService);

        var channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
        channel.Writer.Complete();

        // Act
        var (totalSymbols, totalRels) = await sut.RunConsumer(channel.Reader, 0, "testdb", 100);

        // Assert
        totalSymbols.ShouldBe(0);
        totalRels.ShouldBe(0);
        A.CallTo(() => graphService.FlushFiles(A<IEnumerable<FileMetaData>>._, A<string>._)).MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenResultWithUrlNodes_WhenRunConsumerCalled_ThenFlushesUrlNodes()
    {
        // Arrange
        var graphService = A.Fake<IGraphService>();
        var sut = CreateProcessor(graphService: graphService);

        var channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
        var metadata = new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
        var file = new FileMetaData("key", "file.cs", "file.cs", "hash", metadata, "repo", "ns");
        var urlNodes = new List<UrlNode> { new("dep:pkg", "https://example.com", "example") };

        await channel.Writer.WriteAsync(new SolutionProcessor.ProcessResult(file, [], [], urlNodes, "file.cs"));
        channel.Writer.Complete();

        // Act
        await sut.RunConsumer(channel.Reader, 1, "testdb", 100);

        // Assert
        A.CallTo(() => graphService.UpsertDependencyUrls(A<IEnumerable<UrlNode>>._, "testdb")).MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task GivenProjectAndDocument_WhenProcessFileCalled_ThenHandlesDocument()
    {
        // Arrange
        var fileService = A.Fake<IFileService>();
        var handler = A.Fake<IDocumentHandler>();
        A.CallTo(() => handler.FileExtension).Returns(".cs");
        A.CallTo(() => handler.CanHandle(A<string>._)).Returns(true);
        var sut = CreateProcessor(fileService: fileService, handlers: [handler]);

        var workspace = new AdhocWorkspace();
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
        var document = workspace.AddDocument(project.Id, "test.cs", Microsoft.CodeAnalysis.Text.SourceText.From("content"));
        var solution = workspace.CurrentSolution;

        var processedFile = new ProcessedFile("test.cs", project.Id, document.Id);

        A.CallTo(() => fileService.GetRelativePath(A<string>._, "test.cs")).Returns("test.cs");
        A.CallTo(() => fileService.InferFileMetadata("test.cs")).Returns(("key", "ns"));

        // Act
        var result = await sut.ProcessFile(solution, processedFile, "/root", "repo", Accessibility.Public);

        // Assert
        result.RelativePath.ShouldBe("test.cs");
        A.CallTo(() => handler.Handle(A<TextDocument?>._, A<Compilation?>._, "repo", "key", "test.cs", "test.cs", A<List<Symbol>>._, A<List<Relationship>>._, Accessibility.Public))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public void GivenHandlersWithExtensionsAndFilenames_WhenHandlerLookupCreated_ThenIndexesCorrectly()
    {
        // Arrange
        var h1 = A.Fake<IDocumentHandler>();
        A.CallTo(() => h1.FileExtension).Returns(".cs");
        A.CallTo(() => h1.CanHandle(A<string>.That.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))).Returns(true);

        var h2 = A.Fake<IDocumentHandler>();
        A.CallTo(() => h2.FileExtension).Returns("package.json");
        A.CallTo(() => h2.CanHandle(A<string>.That.EndsWith("package.json", StringComparison.OrdinalIgnoreCase))).Returns(true);

        // Act
        var lookup = new SolutionProcessor.HandlerLookup([h1, h2]);

        // Assert
        lookup.GetHandler("foo.cs").ShouldBe(h1);
        lookup.GetHandler("/path/to/package.json").ShouldBe(h2);
        lookup.GetHandler("other.json").ShouldBeNull();
    }

    private static SolutionProcessor CreateProcessor(
        IGraphService? graphService = null,
        IProgressService? progressService = null,
        IFileService? fileService = null,
        IEnumerable<IDocumentHandler>? handlers = null)
    {
        return new SolutionProcessor(
            A.Fake<IVersionControlService>(),
            graphService ?? A.Fake<IGraphService>(),
            fileService ?? A.Fake<IFileService>(),
            A.Fake<IFileSystem>(),
            progressService ?? A.Fake<IProgressService>(),
            handlers ?? [],
            A.Fake<IDependencyIngestor>(),
            A.Fake<ISolutionFileDiscoveryService>(),
            A.Fake<ICommitIngestionService>(),
            A.Fake<ILogger<SolutionProcessor>>());
    }
}
