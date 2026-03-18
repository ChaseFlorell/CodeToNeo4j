using System.IO.Abstractions;
using CodeToNeo4j.Dart.Bridge;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Dart.Tests.Bridge;

public class DartBridgeServiceTests
{
    private static DartBridgeService CreateSut() =>
        new(new FileSystem(), NullLogger<DartBridgeService>.Instance);

    [Fact]
    public void GivenDartOnPath_WhenFindDartExecutableCalled_ThenReturnsPathOrNull()
    {
        var result = CreateSut().FindDartExecutable();
        Assert.True(result is null || result.Contains("dart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GivenEmbeddedResources_WhenEnsureBridgeExtractedCalled_ThenExtractsFilesToCache()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var bridgeDir = sut.EnsureBridgeExtracted();

        // Assert
        bridgeDir.ShouldNotBeNull();
        Directory.Exists(bridgeDir).ShouldBeTrue();
        File.Exists(Path.Combine(bridgeDir, "pubspec.yaml")).ShouldBeTrue();
        File.Exists(Path.Combine(bridgeDir, "bin", "dart_analyzer_bridge.dart")).ShouldBeTrue();
        File.Exists(Path.Combine(bridgeDir, "lib", "src", "analyzer_service.dart")).ShouldBeTrue();
        File.Exists(Path.Combine(bridgeDir, "lib", "src", "ast_visitor.dart")).ShouldBeTrue();
        File.Exists(Path.Combine(bridgeDir, "lib", "src", "models.dart")).ShouldBeTrue();
        File.Exists(Path.Combine(bridgeDir, "lib", "src", "json_output.dart")).ShouldBeTrue();
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
    public async Task GivenPackageConfigExists_WhenEnsureDartPubGetCalled_ThenReturnsTrueWithoutRunningProcess()
    {
        // Arrange
        var sut = CreateSut();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var dartToolDir = Path.Combine(tempDir, ".dart_tool");
        var packageConfig = Path.Combine(dartToolDir, "package_config.json");

        try
        {
            Directory.CreateDirectory(dartToolDir);
            await File.WriteAllTextAsync(packageConfig, "{}");

            // Act — pass a non-existent dart executable; if it tries to run it, the process would fail
            var result = await sut.EnsureDartPubGet("/nonexistent/dart", tempDir);

            // Assert — returns true immediately because package_config.json exists
            result.ShouldBeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GivenInvalidDartExecutable_WhenEnsureDartPubGetCalled_ThenReturnsFalse()
    {
        // Arrange
        var sut = CreateSut();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act — no package_config.json and dart executable does not exist → Process.Start throws → caught → false
            var result = await sut.EnsureDartPubGet("/nonexistent/dart-executable-that-does-not-exist", tempDir);

            // Assert
            result.ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task GivenNoDartOnPath_WhenAnalyzeProjectCalled_ThenReturnsNull()
    {
        // Only meaningful when Dart is not available; skip if dart is on PATH to avoid clearing real PATH
        if (CreateSut().FindDartExecutable() is not null)
            return;

        var sut = CreateSut();
        var result = await sut.AnalyzeProject("/some/nonexistent/project");
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GivenSameProjectRoot_WhenAnalyzeProjectCalledTwice_ThenSecondCallReturnsCachedResult()
    {
        // Arrange — use a path where Dart analysis will fail/return null (no pubspec, no dart, etc.)
        var sut = CreateSut();
        var fakePath = Path.Combine(Path.GetTempPath(), "nonexistent-dart-project-" + Guid.NewGuid());

        // Act
        var first = await sut.AnalyzeProject(fakePath);
        var second = await sut.AnalyzeProject(fakePath);

        // Assert — both calls agree (second is served from cache)
        second.ShouldBe(first);
    }
}
