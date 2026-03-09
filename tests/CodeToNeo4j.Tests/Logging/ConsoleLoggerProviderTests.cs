using CodeToNeo4j.Logging;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Logging;

public class ConsoleLoggerProviderTests
{
    [Fact]
    public void GivenConsoleLoggerProvider_WhenDisposeCalled_ThenDoesNotThrow()
    {
        // Arrange
        var sut = new ConsoleLoggerProvider(LogLevel.Information);

        // Act & Assert
        Should.NotThrow(() => sut.Dispose());
    }

    [Fact]
    public void GivenConsoleLoggerProvider_WhenCreateLoggerCalled_ThenReturnsConsoleLogger()
    {
        // Arrange
        var sut = new ConsoleLoggerProvider(LogLevel.Information);

        // Act
        var result = sut.CreateLogger("Test");

        // Assert
        result.ShouldBeOfType<ConsoleLogger>();
    }
}
