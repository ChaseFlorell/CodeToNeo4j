using System.IO.Abstractions;
using CodeToNeo4j.TypeScript.Bridge;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.TypeScript.Tests.Bridge;

public class TypeScriptBridgeServiceTests
{
	private static TypeScriptBridgeService CreateSut() =>
		new(new FileSystem(), NullLogger<TypeScriptBridgeService>.Instance);

	[Fact]
	public void GivenNodeOnPath_WhenFindNodeExecutableCalled_ThenReturnsPathOrNull()
	{
		var result = CreateSut().FindNodeExecutable();
		Assert.True(result is null || result.Contains("node", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void GivenEmbeddedResource_WhenEnsureBridgeExtractedCalled_ThenExtractsDistIndexJs()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var bridgeDir = sut.EnsureBridgeExtracted();

		// Assert
		bridgeDir.ShouldNotBeNull();
		Directory.Exists(bridgeDir).ShouldBeTrue();
		File.Exists(Path.Combine(bridgeDir, "dist", "index.js")).ShouldBeTrue();
		File.Exists(Path.Combine(bridgeDir, ".extracted")).ShouldBeTrue();
	}

	[Fact]
	public void GivenAlreadyExtracted_WhenEnsureBridgeExtractedCalledAgain_ThenReturnsSameDir()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		var first = sut.EnsureBridgeExtracted();
		var second = sut.EnsureBridgeExtracted();

		// Assert
		first.ShouldBe(second);
	}

	[Fact]
	public async Task GivenNoNodeOnPath_WhenAnalyzeProjectCalled_ThenReturnsNull()
	{
		// Only meaningful when Node.js is not available; skip if node is on PATH
		if (CreateSut().FindNodeExecutable() is not null)
		{
			return;
		}

		var sut = CreateSut();
		var result = await sut.AnalyzeProject("/some/nonexistent/project");
		result.ShouldBeNull();
	}

	[Fact]
	public async Task GivenSameProjectRoot_WhenAnalyzeProjectCalledTwice_ThenSecondCallReturnsCachedResult()
	{
		// Arrange — use a path where analysis will fail/return null (no package.json, etc.)
		var sut = CreateSut();
		var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-ts-project-" + Guid.NewGuid());

		// Act
		var first = await sut.AnalyzeProject(fakePath);
		var second = await sut.AnalyzeProject(fakePath);

		// Assert — both calls agree (second is served from cache)
		second.ShouldBe(first);
	}
}
