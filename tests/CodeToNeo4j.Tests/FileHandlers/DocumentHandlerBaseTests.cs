using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Graph;
using FakeItEasy;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.FileHandlers;

public class DocumentHandlerBaseTests
{
	[Theory]
	[InlineData("hello\nworld", 0, 1)]
	[InlineData("hello\nworld", 6, 2)]
	[InlineData("line1\nline2\nline3", 12, 3)]
	public void GivenContent_WhenGetLineNumberCalled_ThenReturnsCorrectLine(string content, int index, int expected) =>
		TestHandler.TestGetLineNumber(content, index).ShouldBe(expected);

	[Theory]
	[InlineData(Accessibility.Private, true)]
	[InlineData(Accessibility.Internal, true)]
	[InlineData(Accessibility.Public, true)]
	[InlineData(Accessibility.NotApplicable, false)]
	public void GivenAccessibility_WhenIsPublicAccessibleCalled_ThenReturnsExpected(Accessibility minAccessibility, bool expected) =>
		TestHandler.TestIsPublicAccessible(minAccessibility).ShouldBe(expected);

	[Fact]
	public async Task GivenDocument_WhenGetContentCalled_ThenReturnsDocumentText()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TestHandler sut = new(fileSystem, MakeConfigService());
		AdhocWorkspace workspace = new();
		var project = workspace.AddProject("TestProject", LanguageNames.CSharp);
		var document = workspace.AddDocument(project.Id, "test.cs", SourceText.From("doc content"));

		// Act
		var content = await sut.PublicGetContent(document, "any.cs");

		// Assert
		content.ShouldBe("doc content");
	}

	[Fact]
	public async Task GivenNoDocument_WhenGetContentCalled_ThenReadsFromFileSystem()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		fileSystem.AddFile("file.cs", new("file content"));
		TestHandler sut = new(fileSystem, MakeConfigService());

		// Act
		var content = await sut.PublicGetContent(null, "file.cs");

		// Assert
		content.ShouldBe("file content");
	}

	[Fact]
	public async Task GivenHandlerCalledMultipleTimes_WhenNumberOfFilesHandledRead_ThenReflectsCallCount()
	{
		// Arrange
		MockFileSystem fileSystem = new();
		TestHandler sut = new(fileSystem, MakeConfigService());

		// Act
		await sut.Handle(null, null, null, "a", "a", "a", [], [], Accessibility.NotApplicable);
		await sut.Handle(null, null, null, "b", "b", "b", [], [], Accessibility.NotApplicable);

		// Assert
		sut.NumberOfFilesHandled.ShouldBe(2);
	}

	[Theory]
	[InlineData("file.test", true)]
	[InlineData("FILE.TEST", true)]
	[InlineData("file.cs", false)]
	[InlineData("file.test.bak", false)]
	public void GivenFilePath_WhenCanHandleCalled_ThenReturnsExpected(string filePath, bool expected)
	{
		TestHandler sut = new(new MockFileSystem(), MakeConfigService());
		sut.CanHandle(filePath).ShouldBe(expected);
	}

	private static IConfigurationService MakeConfigService()
	{
		IConfigurationService configService = A.Fake<IConfigurationService>();
		A.CallTo(() => configService.GetHandlerConfiguration(A<string>._))
			.Returns(new HandlerConfiguration(".test", "test"));
		return configService;
	}

	private sealed class TestHandler(MockFileSystem fs, IConfigurationService configService) : DocumentHandlerBase(fs, configService)
	{
		protected override Task<FileResult> HandleFile(
			TextDocument? document,
			Compilation? compilation,
			string? repoKey,
			string fileKey,
			string filePath,
			string relativePath,
			ICollection<Symbol> symbolBuffer,
			ICollection<Relationship> relBuffer,
			Accessibility minAccessibility)
			=> Task.FromResult(new FileResult(null, fileKey));

		public static int TestGetLineNumber(string content, int index) => GetLineNumber(content, index);
		public static bool TestIsPublicAccessible(Accessibility minAccessibility) => IsPublicAccessible(minAccessibility);
		public Task<string> PublicGetContent(TextDocument? document, string filePath) => GetContent(document, filePath);
	}
}
