using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.Solution;
using FakeItEasy;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Solution;

public class HandlerLookupTests
{
	[Theory]
	[InlineData("/repo/src/Program.cs", ".cs")]
	[InlineData("/repo/src/Index.html", ".html")]
	[InlineData("/repo/src/style.css", ".css")]
	[InlineData("/repo/src/App.razor", ".razor")]
	[InlineData("/repo/src/data.xml", ".xml")]
	[InlineData("/repo/src/App.xaml", ".xaml")]
	[InlineData("/repo/src/project.csproj", ".csproj")]
	[InlineData("/repo/src/app.js", ".js")]
	public void GivenExtensionHandler_WhenGetHandlerCalled_ThenReturnsDictionaryMatch(string filePath, string extension)
	{
		// Arrange
		var handler = CreateExtensionHandler(extension);
		SolutionProcessor.HandlerLookup sut = new([handler], new System.IO.Abstractions.FileSystem());

		// Act
		var result = sut.GetHandler(filePath);

		// Assert
		result.ShouldBe(handler);
	}

	[Theory]
	[InlineData("/repo/src/app.ts")]
	[InlineData("/repo/src/Component.tsx")]
	public void GivenTypeScriptHandler_WhenGetHandlerCalled_ThenMatchesTsAndTsx(string filePath)
	{
		// Arrange — handler declares both .ts and .tsx; HandlerLookup indexes all extensions for O(1) lookup
		var tsHandler = A.Fake<IDocumentHandler>();
		A.CallTo(() => tsHandler.FileExtension).Returns(".ts");
		A.CallTo(() => tsHandler.FileExtensions).Returns([".ts", ".tsx"]);
		A.CallTo(() => tsHandler.CanHandle(A<string>._))
			.ReturnsLazily((string path) =>
				path.EndsWith(".ts", StringComparison.OrdinalIgnoreCase)
				|| path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase));
		SolutionProcessor.HandlerLookup sut = new([tsHandler], new System.IO.Abstractions.FileSystem());

		// Act
		var result = sut.GetHandler(filePath);

		// Assert
		result.ShouldBe(tsHandler);
	}

	[Fact]
	public void GivenPackageJsonAndJsonHandlers_WhenGetHandlerCalledWithPackageJson_ThenReturnsPackageJsonHandler()
	{
		// Arrange
		var pkgHandler = CreateFileNameHandler("package.json");
		var jsonHandler = CreateExtensionHandler(".json");
		SolutionProcessor.HandlerLookup sut = new([pkgHandler, jsonHandler], new System.IO.Abstractions.FileSystem());

		// Act
		var result = sut.GetHandler("/repo/package.json");

		// Assert
		result.ShouldBe(pkgHandler);
	}

	[Fact]
	public void GivenPackageJsonAndJsonHandlers_WhenGetHandlerCalledWithRegularJson_ThenReturnsJsonHandler()
	{
		// Arrange
		var pkgHandler = CreateFileNameHandler("package.json");
		var jsonHandler = CreateExtensionHandler(".json");
		SolutionProcessor.HandlerLookup sut = new([pkgHandler, jsonHandler], new System.IO.Abstractions.FileSystem());

		// Act
		var result = sut.GetHandler("/repo/appsettings.json");

		// Assert
		result.ShouldBe(jsonHandler);
	}

	[Fact]
	public void GivenNoMatchingHandler_WhenGetHandlerCalled_ThenReturnsNull()
	{
		// Arrange
		var handler = CreateExtensionHandler(".cs");
		SolutionProcessor.HandlerLookup sut = new([handler], new System.IO.Abstractions.FileSystem());

		// Act
		var result = sut.GetHandler("/repo/file.unknown");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GivenCaseInsensitiveExtension_WhenGetHandlerCalled_ThenReturnsMatch()
	{
		// Arrange
		var handler = CreateExtensionHandler(".cs");
		SolutionProcessor.HandlerLookup sut = new([handler], new System.IO.Abstractions.FileSystem());

		// Act
		var result = sut.GetHandler("/repo/Program.CS");

		// Assert
		result.ShouldBe(handler);
	}

	private static IDocumentHandler CreateExtensionHandler(string extension, Func<string, bool>? canHandle = null)
	{
		var handler = A.Fake<IDocumentHandler>();
		A.CallTo(() => handler.FileExtension).Returns(extension);
		A.CallTo(() => handler.FileExtensions).Returns([extension]);
		A.CallTo(() => handler.CanHandle(A<string>._))
			.ReturnsLazily((string path) => canHandle?.Invoke(path)
											?? path.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
		return handler;
	}

	private static IDocumentHandler CreateFileNameHandler(string fileName)
	{
		var handler = A.Fake<IDocumentHandler>();
		A.CallTo(() => handler.FileExtension).Returns(fileName);
		A.CallTo(() => handler.FileExtensions).Returns([fileName]);
		A.CallTo(() => handler.CanHandle(A<string>._))
			.ReturnsLazily((string path) => path.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
		return handler;
	}
}
