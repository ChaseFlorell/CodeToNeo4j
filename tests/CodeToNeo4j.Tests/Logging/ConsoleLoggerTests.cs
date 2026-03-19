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
		ConsoleLogger sut = new("Test", LogLevel.Information);

		// Act & Assert
		sut.IsEnabled(LogLevel.Information).ShouldBeTrue();
		sut.IsEnabled(LogLevel.Warning).ShouldBeTrue();
		sut.IsEnabled(LogLevel.Debug).ShouldBeFalse();
	}

	[Fact]
	public void GivenConsoleLogger_WhenLogCalled_ThenDoesNotThrow()
	{
		// Arrange
		ConsoleLogger sut = new("Test", LogLevel.Information);

		// Act & Assert
		Should.NotThrow(() => sut.Log(LogLevel.Information, new(1), "state", null, (s, e) => s.ToString()));
	}

	[Fact]
	public void GivenConsoleLogger_WhenLogCalled_ThenIncludesThreadTagWithCorrectFormat()
	{
		// Arrange
		StringWriter output = new();
		Console.SetOut(output);
		ConsoleLogger sut = new("TestLogger", LogLevel.Information);

		// Act
		sut.Log(LogLevel.Information, new(0), "Test message", null, (s, e) => s);

		// Assert
		var logOutput = output.ToString();
		logOutput.ShouldContain("TestLogger");
		// Check for exact formatting: [INFO][TAG-N]
		logOutput.ShouldMatch(@".*\[INFO\]\[(MAIN|TASK|FINL)-\d{3}\] TestLogger Test message.*");
	}

	[Fact]
	public void GivenConsoleLogger_WhenLogCalled_ThenThreadIdIsZeroPaddedToThreeDigits()
	{
		// Arrange
		StringWriter output = new();
		Console.SetOut(output);
		ConsoleLogger sut = new("Test", LogLevel.Information);

		// Act
		sut.Log(LogLevel.Information, new(0), "msg", null, (s, e) => s);

		// Assert — thread tag must contain exactly 3 digits after the prefix
		var logOutput = output.ToString();
		logOutput.ShouldMatch(@"\[(MAIN|TASK|FINL)-\d{3}\]");
	}

	[Fact]
	public void GivenConsoleLogger_WhenBeginScopeCalled_ThenReturnsNull()
	{
		ConsoleLogger sut = new("Test", LogLevel.Information);
		sut.BeginScope("some-scope").ShouldBeNull();
	}

	[Fact]
	public void GivenDisabledLogLevel_WhenLogCalled_ThenProducesNoOutput()
	{
		// Arrange
		StringWriter output = new();
		Console.SetOut(output);
		ConsoleLogger sut = new("Test", LogLevel.Warning);

		// Act — Debug is below minimum Warning, so early-return fires
		sut.Log(LogLevel.Debug, new(0), "should not appear", null, (s, e) => s);

		// Assert
		output.ToString().ShouldBeEmpty();
	}

	[Fact]
	public void GivenProgressMessage_WhenLogCalled_ThenWritesWithCarriageReturn()
	{
		// Arrange
		StringWriter output = new();
		Console.SetOut(output);
		ConsoleLogger sut = new("Test", LogLevel.Information);

		// Act — message containing "[Progress" at Information level hits the \r branch
		sut.Log(LogLevel.Information, new(0), "[Progress: 50%] (5/10)", null, (s, e) => s);

		// Assert — Console.Write (not WriteLine) means no trailing newline from the logger itself
		var written = output.ToString();
		written.ShouldContain("[Progress");
		written.ShouldStartWith("\r");
	}

	[Fact]
	public void GivenExceptionInLog_WhenLogCalled_ThenWritesExceptionToErrorStream()
	{
		// Arrange
		StringWriter errorOutput = new();
		Console.SetError(errorOutput);
		ConsoleLogger sut = new("Test", LogLevel.Error);
		InvalidOperationException ex = new("boom");

		// Act
		sut.Log(LogLevel.Error, new(0), "error occurred", ex, (s, e) => s.ToString());

		// Assert
		errorOutput.ToString().ShouldContain("InvalidOperationException");
		errorOutput.ToString().ShouldContain("boom");
	}

	[Fact]
	public void GivenCiFormattedMessage_WhenLogCalled_ThenUsesCiFormat()
	{
		// Arrange
		StringWriter output = new();
		Console.SetOut(output);
		ConsoleLogger sut = new("MyLogger", LogLevel.Information);

		// Act — message starting with "::" triggers the CI branch (no timestamp)
		sut.Log(LogLevel.Information, new(0), "::notice:: build complete", null, (s, e) => s);

		// Assert — CI format includes name + thread tag but no HH:mm:ss timestamp
		var written = output.ToString();
		written.ShouldContain("MyLogger");
		written.ShouldNotMatch(@"\d{2}:\d{2}:\d{2}");
	}
}
