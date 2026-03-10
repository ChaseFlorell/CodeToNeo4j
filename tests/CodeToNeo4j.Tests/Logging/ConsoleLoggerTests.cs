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

    [Fact]
    public void GivenConsoleLogger_WhenLogCalled_ThenIncludesThreadTagWithCorrectFormat()
    {
        // Arrange
        var output = new StringWriter();
        Console.SetOut(output);
        var sut = new ConsoleLogger("TestLogger", LogLevel.Information);

        // Act
        sut.Log(LogLevel.Information, new EventId(0), "Test message", null, (s, e) => s);

        // Assert
        var logOutput = output.ToString();
        logOutput.ShouldContain("TestLogger");
        // Check for exact formatting: [INFO][TAG-N]
        logOutput.ShouldMatch(@".*\[INFO\]\[(MAIN|TASK|FINL)-\d+\] TestLogger Test message.*");
    }
}
