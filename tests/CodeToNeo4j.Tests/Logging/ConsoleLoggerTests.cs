using CodeToNeo4j.Logging;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Logging;

public class ConsoleLoggerTests
{
    [Fact]
    public void GivenConsoleLogger_WhenIsEnabledCalledWithHigherLogLevel_ThenReturnsTrue()
    {
        // Arrange
        var sut = new ConsoleLogger("Test", LogLevel.Information);

        // Act & Assert
        sut.IsEnabled(LogLevel.Information).ShouldBeTrue();
        sut.IsEnabled(LogLevel.Warning).ShouldBeTrue();
        sut.IsEnabled(LogLevel.Debug).ShouldBeFalse();
    }

    [Fact]
    public void GivenConsoleLogger_WhenLogCalled_ThenDoesNotThrow()
    {
        // Arrange
        var sut = new ConsoleLogger("Test", LogLevel.Information);

        // Act & Assert
        Should.NotThrow(() => sut.Log(LogLevel.Information, new EventId(1), "state", null, (s, e) => s.ToString()));
    }
}
