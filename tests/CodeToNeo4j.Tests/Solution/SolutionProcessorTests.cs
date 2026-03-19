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
		var files = new[] { new ProcessedFile("file1.cs"), new ProcessedFile("file2.cs"), new ProcessedFile("file3.cs") };

		// Act
		var result = SolutionProcessor.FilterFiles(files, null);

		// Assert
		result.Length.ShouldBe(3);
	}

	[Fact]
	public void GivenEmptyChangedFiles_WhenFilterFilesCalled_ThenReturnsEmptyArray()
	{
		// Arrange
		var files = new[] { new ProcessedFile("file1.cs"), new ProcessedFile("file2.cs") };
		HashSet<string> changedFiles = new();

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
		var files = new[] { new ProcessedFile("file1.cs"), new ProcessedFile("file2.cs"), new ProcessedFile("file3.cs") };
		HashSet<string> changedFiles = new() { changedFile };

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
		var sut = CreateProcessor(graphService, progressService);

		Channel<SolutionProcessor.ProcessResult> channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
		FileMetadata metadata = new(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
		FileMetaData fileMetaData = new("key", "file.cs", "file.cs", "hash", metadata, "repo", "ns");
		List<Symbol> symbols = new() { new("k1", "Foo", "NamedType", "class", "Foo", "Public", "key", "file.cs", 1, 10, null, null, "ns") };
		List<Relationship> rels = new() { new("k1", "k2", "CONTAINS") };

		SolutionProcessor.ProcessResult result = new(fileMetaData, symbols, rels, [], "file.cs");
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
		var sut = CreateProcessor(graphService);

		Channel<SolutionProcessor.ProcessResult> channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
		FileMetadata metadata = new(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);

		// Write 3 results with batchSize=2 — should flush twice (once at threshold, once at end)
		for (var i = 0; i < 3; i++)
		{
			FileMetaData file = new($"key{i}", $"file{i}.cs", $"file{i}.cs", "hash", metadata, "repo", "ns");
			List<Symbol> symbols = new()
			{
				new($"s{i}", $"Sym{i}", "NamedType", "class", $"Sym{i}", "Public", $"key{i}", $"file{i}.cs", 1, 10, null, null, "ns")
			};
			await channel.Writer.WriteAsync(new(file, symbols, [], [], $"file{i}.cs"));
		}

		channel.Writer.Complete();

		// Act
		var (totalSymbols, _) = await sut.RunConsumer(channel.Reader, 3, "testdb", 2);

		// Assert
		totalSymbols.ShouldBe(3);
		A.CallTo(() => graphService.FlushFiles(A<IEnumerable<FileMetaData>>._, "testdb")).MustHaveHappened(2, Times.Exactly);
		A.CallTo(() => graphService.FlushSymbols(A<IEnumerable<Symbol>>._, A<IEnumerable<Relationship>>._, "testdb"))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task GivenEmptyChannel_WhenRunConsumerCalled_ThenReturnsZeroTotals()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		var sut = CreateProcessor(graphService);

		Channel<SolutionProcessor.ProcessResult> channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
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
		var sut = CreateProcessor(graphService);

		Channel<SolutionProcessor.ProcessResult> channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
		FileMetadata metadata = new(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
		FileMetaData file = new("key", "file.cs", "file.cs", "hash", metadata, "repo", "ns");
		List<UrlNode> urlNodes = new() { new("dep:pkg", "https://example.com", "example") };

		await channel.Writer.WriteAsync(new(file, [], [], urlNodes, "file.cs"));
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

		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
		var document = workspace.AddDocument(project.Id, "test.cs", Microsoft.CodeAnalysis.Text.SourceText.From("content"));
		var solution = workspace.CurrentSolution;

		ProcessedFile processedFile = new("test.cs", project.Id, document.Id);

		A.CallTo(() => fileService.GetRelativePath(A<string>._, "test.cs")).Returns("test.cs");
		A.CallTo(() => fileService.InferFileMetadata("test.cs")).Returns(("key", "ns"));

		// Act
		var result = await sut.ProcessFile(solution, processedFile, "/root", "repo", Accessibility.Public);

		// Assert
		result.RelativePath.ShouldBe("test.cs");
		A.CallTo(() => handler.Handle(A<TextDocument?>._, A<Compilation?>._, "repo", "key", "test.cs", "test.cs", A<List<Symbol>>._,
				A<List<Relationship>>._, Accessibility.Public))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenResultWithTargetFrameworks_WhenRunConsumerCalled_ThenFlushesTargetFrameworks()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		var sut = CreateProcessor(graphService);

		Channel<SolutionProcessor.ProcessResult> channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
		FileMetadata metadata = new(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
		FileMetaData file = new("key", "file.cs", "file.cs", "hash", metadata, "repo", "ns");
		List<Symbol> symbols = new() { new("k1", "Foo", "NamedType", "class", "Foo", "Public", "key", "file.cs", 1, 10, null, null, "ns") };
		IReadOnlySet<string> tfms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "net9.0", "net8.0" } as IReadOnlySet<string>;

		await channel.Writer.WriteAsync(new(file, symbols, [], [], "file.cs", tfms));
		channel.Writer.Complete();

		// Act
		await sut.RunConsumer(channel.Reader, 1, "testdb", 100);

		// Assert
		A.CallTo(() => graphService.FlushTargetFrameworks(A<IEnumerable<TargetFrameworkBatch>>._, "testdb")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenResultWithNoTargetFrameworks_WhenRunConsumerCalled_ThenDoesNotFlushTargetFrameworks()
	{
		// Arrange
		var graphService = A.Fake<IGraphService>();
		var sut = CreateProcessor(graphService);

		Channel<SolutionProcessor.ProcessResult> channel = Channel.CreateUnbounded<SolutionProcessor.ProcessResult>();
		FileMetadata metadata = new(DateTimeOffset.Now, DateTimeOffset.Now, [], [], []);
		FileMetaData file = new("key", "file.cs", "file.cs", "hash", metadata, "repo", "ns");

		await channel.Writer.WriteAsync(new(file, [], [], [], "file.cs"));
		channel.Writer.Complete();

		// Act
		await sut.RunConsumer(channel.Reader, 1, "testdb", 100);

		// Assert
		A.CallTo(() => graphService.FlushTargetFrameworks(A<IEnumerable<TargetFrameworkBatch>>._, A<string>._)).MustNotHaveHappened();
	}

	// ── ProcessSolution tests ────────────────────────────────────────────

	[Fact]
	public async Task GivenDirectoryPath_WhenProcessSolutionCalled_ThenRunsInFilesOnlyMode()
	{
		// arrange
		var graphService = A.Fake<IGraphService>();
		var fileService = A.Fake<IFileService>();
		var fileSystem = A.Fake<IFileSystem>();
		var vcs = A.Fake<IVersionControlService>();
		var discoveryService = A.Fake<ISolutionFileDiscoveryService>();
		var dependencyIngestor = A.Fake<IDependencyIngestor>();
		var workspaceFactory = A.Fake<IWorkspaceFactory>();
		var handler = A.Fake<IDocumentHandler>();
		A.CallTo(() => handler.FileExtension).Returns(".cs");
		A.CallTo(() => handler.CanHandle(A<string>._)).Returns(true);
		A.CallTo(() => handler.NumberOfFilesHandled).Returns(1);

		A.CallTo(() => fileSystem.Path.GetExtension("/repo")).Returns("");
		A.CallTo(() => fileSystem.Path.GetFileName(A<string>._)).Returns("repo");
		A.CallTo(() => fileSystem.Path.DirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Path.AltDirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileService.NormalizePath("/repo")).Returns("/repo");
		A.CallTo(() => fileService.GetRelativePath("/repo", "/repo/file.cs")).Returns("file.cs");
		A.CallTo(() => fileService.InferFileMetadata("file.cs")).Returns(("key", "ns"));
		A.CallTo(() => fileService.ComputeSha256("/repo/file.cs")).Returns(Task.FromResult("hash"));
		A.CallTo(() => vcs.LoadMetadata("/repo", A<HashSet<string>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => vcs.GetFileMetadata("/repo/file.cs", "/repo"))
			.Returns(Task.FromResult(new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], [])));
		A.CallTo(() => discoveryService.GetFilesToProcess("/repo", null, A<IEnumerable<string>>._))
			.Returns([new("/repo/file.cs")]);

		var sut = CreateProcessor(
			graphService,
			fileService: fileService,
			fileSystem: fileSystem,
			versionControlService: vcs,
			discoveryService: discoveryService,
			dependencyIngestor: dependencyIngestor,
			workspaceFactory: workspaceFactory,
			handlers: [handler]);

		// act
		await sut.ProcessSolution("/repo", "repo", null, "testdb", 100, false, Accessibility.Public, [".cs"]);

		// assert — workspace never created, dependencies never ingested, files still processed
		A.CallTo(() => workspaceFactory.Create()).MustNotHaveHappened();
		A.CallTo(() => dependencyIngestor.IngestDependencies(A<Microsoft.CodeAnalysis.Solution>._, A<string?>._, A<string>._!)).MustNotHaveHappened();
		A.CallTo(() => graphService.FlushFiles(A<IEnumerable<FileMetaData>>._, "testdb")).MustHaveHappenedOnceExactly();
	}

	[Theory]
	[InlineData(".sln")]
	[InlineData(".slnx")]
	public async Task GivenSolutionFile_WhenProcessSolutionCalled_ThenOpensWorkspaceAsSolution(string ext)
	{
		// arrange
		var graphService = A.Fake<IGraphService>();
		var fileService = A.Fake<IFileService>();
		var fileSystem = A.Fake<IFileSystem>();
		var vcs = A.Fake<IVersionControlService>();
		var discoveryService = A.Fake<ISolutionFileDiscoveryService>();
		var workspaceFactory = A.Fake<IWorkspaceFactory>();
		var workspace = A.Fake<IManagedWorkspace>();
		var dependencyIngestor = A.Fake<IDependencyIngestor>();

		var inputPath = $"/repo/MySolution{ext}";
		AdhocWorkspace adhocWorkspace = new();
		var fakeSolution = adhocWorkspace.CurrentSolution;

		A.CallTo(() => fileSystem.Path.GetExtension(inputPath)).Returns(ext);
		A.CallTo(() => fileSystem.Path.GetDirectoryName(inputPath)).Returns("/repo");
		A.CallTo(() => fileSystem.Path.GetFileName(A<string>._)).Returns($"MySolution{ext}");
		A.CallTo(() => fileSystem.Path.DirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Path.AltDirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Directory.GetCurrentDirectory()).Returns("/repo");
		A.CallTo(() => fileService.NormalizePath("/repo")).Returns("/repo");
		A.CallTo(() => vcs.LoadMetadata("/repo", A<HashSet<string>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => workspaceFactory.Create()).Returns(workspace);
		A.CallTo(() => workspace.OpenSolutionAsync(inputPath)).Returns(Task.FromResult(fakeSolution));
		A.CallTo(() => discoveryService.GetFilesToProcess("/repo", fakeSolution, A<IEnumerable<string>>._))
			.Returns([]);

		var sut = CreateProcessor(
			graphService,
			fileService: fileService,
			fileSystem: fileSystem,
			versionControlService: vcs,
			discoveryService: discoveryService,
			dependencyIngestor: dependencyIngestor,
			workspaceFactory: workspaceFactory);

		// act
		await sut.ProcessSolution(inputPath, "mysolution", null, "testdb", 100, false, Accessibility.Public, [".cs"]);

		// assert
		A.CallTo(() => workspaceFactory.Create()).MustHaveHappenedOnceExactly();
		A.CallTo(() => workspace.OpenSolutionAsync(inputPath)).MustHaveHappenedOnceExactly();
		A.CallTo(() => workspace.OpenProjectAsync(A<string>._)).MustNotHaveHappened();
		A.CallTo(() => workspace.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenCsprojFile_WhenProcessSolutionCalled_ThenOpensWorkspaceAsProject()
	{
		// arrange
		var graphService = A.Fake<IGraphService>();
		var fileService = A.Fake<IFileService>();
		var fileSystem = A.Fake<IFileSystem>();
		var vcs = A.Fake<IVersionControlService>();
		var discoveryService = A.Fake<ISolutionFileDiscoveryService>();
		var workspaceFactory = A.Fake<IWorkspaceFactory>();
		var workspace = A.Fake<IManagedWorkspace>();
		var dependencyIngestor = A.Fake<IDependencyIngestor>();

		var inputPath = "/repo/MyProject.csproj";
		AdhocWorkspace adhocWorkspace = new();
		var fakeSolution = adhocWorkspace.CurrentSolution;

		A.CallTo(() => fileSystem.Path.GetExtension(inputPath)).Returns(".csproj");
		A.CallTo(() => fileSystem.Path.GetDirectoryName(inputPath)).Returns("/repo");
		A.CallTo(() => fileSystem.Path.GetFileName(A<string>._)).Returns("MyProject.csproj");
		A.CallTo(() => fileSystem.Path.DirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Path.AltDirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Directory.GetCurrentDirectory()).Returns("/repo");
		A.CallTo(() => fileService.NormalizePath("/repo")).Returns("/repo");
		A.CallTo(() => vcs.LoadMetadata("/repo", A<HashSet<string>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => workspaceFactory.Create()).Returns(workspace);
		A.CallTo(() => workspace.OpenProjectAsync(inputPath)).Returns(Task.FromResult(fakeSolution));
		A.CallTo(() => discoveryService.GetFilesToProcess("/repo", fakeSolution, A<IEnumerable<string>>._))
			.Returns([]);

		var sut = CreateProcessor(
			graphService,
			fileService: fileService,
			fileSystem: fileSystem,
			versionControlService: vcs,
			discoveryService: discoveryService,
			dependencyIngestor: dependencyIngestor,
			workspaceFactory: workspaceFactory);

		// act
		await sut.ProcessSolution(inputPath, "myproject", null, "testdb", 100, false, Accessibility.Public, [".cs"]);

		// assert
		A.CallTo(() => workspace.OpenProjectAsync(inputPath)).MustHaveHappenedOnceExactly();
		A.CallTo(() => workspace.OpenSolutionAsync(A<string>._)).MustNotHaveHappened();
		A.CallTo(() => workspace.Dispose()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenNoFilesDiscovered_WhenProcessSolutionCalled_ThenReturnsEarlyWithoutProcessing()
	{
		// arrange
		var graphService = A.Fake<IGraphService>();
		var fileService = A.Fake<IFileService>();
		var fileSystem = A.Fake<IFileSystem>();
		var vcs = A.Fake<IVersionControlService>();
		var discoveryService = A.Fake<ISolutionFileDiscoveryService>();

		A.CallTo(() => fileSystem.Path.GetExtension("/repo")).Returns("");
		A.CallTo(() => fileService.NormalizePath("/repo")).Returns("/repo");
		A.CallTo(() => vcs.LoadMetadata("/repo", A<HashSet<string>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => discoveryService.GetFilesToProcess("/repo", null, A<IEnumerable<string>>._))
			.Returns([]);

		var sut = CreateProcessor(
			graphService,
			fileService: fileService,
			fileSystem: fileSystem,
			versionControlService: vcs,
			discoveryService: discoveryService);

		// act
		await sut.ProcessSolution("/repo", "repo", null, "testdb", 100, false, Accessibility.Public, [".cs"]);

		// assert — no flushing happened
		A.CallTo(() => graphService.FlushFiles(A<IEnumerable<FileMetaData>>._, A<string>._)).MustNotHaveHappened();
		A.CallTo(() => graphService.FlushSymbols(A<IEnumerable<Symbol>>._, A<IEnumerable<Relationship>>._, A<string>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenSkipDependenciesTrue_WhenProcessSolutionCalled_ThenSkipsDependencyIngestion()
	{
		// arrange
		var fileService = A.Fake<IFileService>();
		var fileSystem = A.Fake<IFileSystem>();
		var vcs = A.Fake<IVersionControlService>();
		var discoveryService = A.Fake<ISolutionFileDiscoveryService>();
		var workspaceFactory = A.Fake<IWorkspaceFactory>();
		var workspace = A.Fake<IManagedWorkspace>();
		var dependencyIngestor = A.Fake<IDependencyIngestor>();

		AdhocWorkspace adhocWorkspace = new();
		var fakeSolution = adhocWorkspace.CurrentSolution;

		A.CallTo(() => fileSystem.Path.GetExtension("/repo/My.sln")).Returns(".sln");
		A.CallTo(() => fileSystem.Path.GetDirectoryName("/repo/My.sln")).Returns("/repo");
		A.CallTo(() => fileSystem.Path.GetFileName(A<string>._)).Returns("My.sln");
		A.CallTo(() => fileSystem.Path.DirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Path.AltDirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Directory.GetCurrentDirectory()).Returns("/repo");
		A.CallTo(() => fileService.NormalizePath("/repo")).Returns("/repo");
		A.CallTo(() => vcs.LoadMetadata("/repo", A<HashSet<string>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => workspaceFactory.Create()).Returns(workspace);
		A.CallTo(() => workspace.OpenSolutionAsync("/repo/My.sln")).Returns(Task.FromResult(fakeSolution));
		A.CallTo(() => discoveryService.GetFilesToProcess("/repo", fakeSolution, A<IEnumerable<string>>._))
			.Returns([]);

		var sut = CreateProcessor(
			fileService: fileService,
			fileSystem: fileSystem,
			versionControlService: vcs,
			discoveryService: discoveryService,
			dependencyIngestor: dependencyIngestor,
			workspaceFactory: workspaceFactory);

		// act
		await sut.ProcessSolution("/repo/My.sln", "my", null, "testdb", 100, true, Accessibility.Public, [".cs"]);

		// assert
		A.CallTo(() => dependencyIngestor.IngestDependencies(A<Microsoft.CodeAnalysis.Solution>._, A<string?>._, A<string>._!)).MustNotHaveHappened();
	}

	[Fact]
	public async Task GivenDiffBase_WhenProcessSolutionCalled_ThenIngestsCommits()
	{
		// arrange
		var fileService = A.Fake<IFileService>();
		var fileSystem = A.Fake<IFileSystem>();
		var vcs = A.Fake<IVersionControlService>();
		var discoveryService = A.Fake<ISolutionFileDiscoveryService>();
		var commitIngestionService = A.Fake<ICommitIngestionService>();
		var graphService = A.Fake<IGraphService>();
		var handler = A.Fake<IDocumentHandler>();
		A.CallTo(() => handler.FileExtension).Returns(".cs");
		A.CallTo(() => handler.CanHandle(A<string>._)).Returns(true);
		A.CallTo(() => handler.NumberOfFilesHandled).Returns(1);

		A.CallTo(() => fileSystem.Path.GetExtension("/repo")).Returns("");
		A.CallTo(() => fileSystem.Path.GetFileName(A<string>._)).Returns("repo");
		A.CallTo(() => fileSystem.Path.DirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileSystem.Path.AltDirectorySeparatorChar).Returns('/');
		A.CallTo(() => fileService.NormalizePath("/repo")).Returns("/repo");
		A.CallTo(() => fileService.GetRelativePath("/repo", "/repo/file.cs")).Returns("file.cs");
		A.CallTo(() => fileService.InferFileMetadata("file.cs")).Returns(("key", "ns"));
		A.CallTo(() => fileService.ComputeSha256("/repo/file.cs")).Returns(Task.FromResult("hash"));
		A.CallTo(() => vcs.LoadMetadata("/repo", A<HashSet<string>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => vcs.GetFileMetadata("/repo/file.cs", "/repo"))
			.Returns(Task.FromResult(new FileMetadata(DateTimeOffset.Now, DateTimeOffset.Now, [], [], [])));
		A.CallTo(() => vcs.GetChangedFiles("origin/main", "/repo", A<HashSet<string>>._))
			.Returns(Task.FromResult(new DiffResult(["/repo/file.cs"], [], [])));
		A.CallTo(() => discoveryService.GetFilesToProcess("/repo", null, A<IEnumerable<string>>._))
			.Returns([new("/repo/file.cs")]);

		var sut = CreateProcessor(
			graphService,
			fileService: fileService,
			fileSystem: fileSystem,
			versionControlService: vcs,
			discoveryService: discoveryService,
			commitIngestionService: commitIngestionService,
			handlers: [handler]);

		// act
		await sut.ProcessSolution("/repo", "repo", "origin/main", "testdb", 100, false, Accessibility.Public, [".cs"]);

		// assert
		A.CallTo(() => commitIngestionService.IngestCommits("origin/main", "/repo", "repo", "testdb", 100)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task GivenDiffBaseWithDeletedFiles_WhenProcessSolutionCalled_ThenMarksFilesAsDeleted()
	{
		// arrange
		var graphService = A.Fake<IGraphService>();
		var fileService = A.Fake<IFileService>();
		var fileSystem = A.Fake<IFileSystem>();
		var vcs = A.Fake<IVersionControlService>();
		var discoveryService = A.Fake<ISolutionFileDiscoveryService>();

		A.CallTo(() => fileSystem.Path.GetExtension("/repo")).Returns("");
		A.CallTo(() => fileService.NormalizePath("/repo")).Returns("/repo");
		A.CallTo(() => vcs.LoadMetadata("/repo", A<HashSet<string>>._)).Returns(Task.CompletedTask);
		A.CallTo(() => vcs.GetChangedFiles("origin/main", "/repo", A<HashSet<string>>._))
			.Returns(Task.FromResult(new DiffResult([], ["/repo/deleted.cs"], [])));
		A.CallTo(() => fileService.GetRelativePath("/repo", "/repo/deleted.cs")).Returns("deleted.cs");
		A.CallTo(() => discoveryService.GetFilesToProcess("/repo", null, A<IEnumerable<string>>._))
			.Returns([]);

		var sut = CreateProcessor(
			graphService,
			fileService: fileService,
			fileSystem: fileSystem,
			versionControlService: vcs,
			discoveryService: discoveryService);

		// act
		await sut.ProcessSolution("/repo", "repo", "origin/main", "testdb", 100, false, Accessibility.Public, [".cs"]);

		// assert
		A.CallTo(() => graphService.MarkFileAsDeleted("deleted.cs", "testdb")).MustHaveHappenedOnceExactly();
	}

	// ── HandlerLookup tests ────────────────────────────────────────────

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
		SolutionProcessor.HandlerLookup lookup = new([h1, h2], new System.IO.Abstractions.FileSystem());

		// Assert
		lookup.GetHandler("foo.cs").ShouldBe(h1);
		lookup.GetHandler("/path/to/package.json").ShouldBe(h2);
		lookup.GetHandler("other.json").ShouldBeNull();
	}

	private static SolutionProcessor CreateProcessor(
		IGraphService? graphService = null,
		IProgressService? progressService = null,
		IFileService? fileService = null,
		IEnumerable<IDocumentHandler>? handlers = null,
		IVersionControlService? versionControlService = null,
		IFileSystem? fileSystem = null,
		IDependencyIngestor? dependencyIngestor = null,
		ISolutionFileDiscoveryService? discoveryService = null,
		ICommitIngestionService? commitIngestionService = null,
		IWorkspaceFactory? workspaceFactory = null)
	{
		return new(
			versionControlService ?? A.Fake<IVersionControlService>(),
			graphService ?? A.Fake<IGraphService>(),
			fileService ?? A.Fake<IFileService>(),
			fileSystem ?? A.Fake<IFileSystem>(),
			progressService ?? A.Fake<IProgressService>(),
			handlers ?? [],
			dependencyIngestor ?? A.Fake<IDependencyIngestor>(),
			discoveryService ?? A.Fake<ISolutionFileDiscoveryService>(),
			commitIngestionService ?? A.Fake<ICommitIngestionService>(),
			workspaceFactory ?? A.Fake<IWorkspaceFactory>(),
			A.Fake<ILogger<SolutionProcessor>>());
	}
}
