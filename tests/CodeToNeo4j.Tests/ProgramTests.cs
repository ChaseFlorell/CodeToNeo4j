using System.CommandLine;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests;

public class ProgramTests
{
    [Fact]
    public void GivenNoArguments_WhenParsing_ThenShouldHaveValidationErrors()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();

        // act
        var result = sut.Parse();

        // assert
        result.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void GivenValidArguments_WhenParsing_ThenShouldNotHaveErrors()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--sln", "test.sln", "--password", "pass" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenMultipleLogOptions_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--sln", "test.sln", "--password", "pass", "--debug", "--quiet" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "Only one of --log-level, --debug, --verbose, or --quiet can be used.");
    }

    [Fact]
    public void GivenPurgeWithSkipDependencies_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--sln", "test.sln", "--purge-data", "--password", "pass", "--skip-dependencies" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--skip-dependencies is not allowed when using --purge-data");
    }

    [Fact]
    public void GivenPurgeWithMinAccessibility_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--sln", "test.sln", "--purge-data", "--password", "pass", "--min-accessibility", "Public" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--min-accessibility is not allowed when using --purge-data");
    }

    [Fact]
    public void GivenNoSlnAndNotPurge_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--password", "pass" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--sln is required");
    }

    [Theory]
    [InlineData("--debug")]
    [InlineData("--verbose")]
    [InlineData("--quiet")]
    [InlineData("--log-level Information")]
    [InlineData("--log-level Warning")]
    public void GivenLogOptions_WhenParsing_ThenShouldNotHaveErrors(string logArg)
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = $"--sln test.sln --password pass {logArg}".Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenNoKeyAndNoSlnAndNotPurge_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--no-key", "--password", "pass" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--sln is required");
    }

    [Fact]
    public void GivenNoSlnAndNoKeyAndPurge_WhenParsing_ThenShouldNotHaveErrors()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--no-key", "--purge-data", "--password", "pass" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
    }
}
