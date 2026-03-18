using CodeToNeo4j.Logging;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Logging;

public class LoggingExtensionsTests
{
    [Theory]
    [InlineData(LogLevel.None, "[NONE]")]
    [InlineData(LogLevel.Trace, "[VERB]")]
    [InlineData(LogLevel.Debug, "[DEBUG]")]
    [InlineData(LogLevel.Information, "[INFO]")]
    [InlineData(LogLevel.Warning, "[WARN]")]
    [InlineData(LogLevel.Error, "[ERR]")]
    [InlineData(LogLevel.Critical, "[CRIT]")]
    public void GivenKnownLogLevel_WhenTruncateCalled_ThenReturnsExpectedAbbreviation(LogLevel level, string expected)
    {
        level.Truncate().ShouldBe(expected);
    }

    [Fact]
    public void GivenUnknownLogLevel_WhenTruncateCalled_ThenReturnsFallbackWithFirstThreeChars()
    {
        // 100 → ToString() == "100" (3 chars), safe for the [..3] slice in the fallback arm
        var unknown = (LogLevel)100;
        var result = unknown.Truncate();
        result.ShouldBe("[100]");
    }
}
