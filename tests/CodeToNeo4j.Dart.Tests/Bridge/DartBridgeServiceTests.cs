using CodeToNeo4j.Dart.Bridge;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Dart.Tests.Bridge;

public class DartBridgeServiceTests
{
    [Fact]
    public void GivenDartOnPath_WhenFindDartExecutableCalled_ThenReturnsPathOrNull()
    {
        var result = DartBridgeService.FindDartExecutable();
        Assert.True(result is null || result.Contains("dart", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GivenEmbeddedResources_WhenEnsureBridgeExtractedCalled_ThenExtractsFilesToCache()
    {
        // Arrange
        var sut = new DartBridgeService(NullLogger<DartBridgeService>.Instance);

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
        var sut = new DartBridgeService(NullLogger<DartBridgeService>.Instance);

        // Act
        var first = sut.EnsureBridgeExtracted();
        var second = sut.EnsureBridgeExtracted();

        // Assert
        first.ShouldBe(second);
    }
}
