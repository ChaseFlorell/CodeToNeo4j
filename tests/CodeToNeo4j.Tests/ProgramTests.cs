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
        var args = new[] { "--sln", "test.sln", "--password", "pass", "--repository-key", "repo" };

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
        var args = new[] { "--sln", "test.sln", "--password", "pass", "--repository-key", "repo", "--debug", "--quiet" };

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
        var args = new[] { "--purge-data-by-repository-key", "--password", "pass", "--repository-key", "repo", "--skip-dependencies" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--skip-dependencies is not allowed when using --purge-data-by-repository-key");
    }

    [Fact]
    public void GivenPurgeWithMinAccessibility_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--purge-data-by-repository-key", "--password", "pass", "--repository-key", "repo", "--min-accessibility", "Public" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--min-accessibility is not allowed when using --purge-data-by-repository-key");
    }

    [Fact]
    public void GivenNoSlnAndNotPurge_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--password", "pass", "--repository-key", "repo" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--sln is required when not using --purge-data-by-repository-key");
    }

    [Theory]
    [InlineData("--debug")]
    [InlineData("--verbose")]
    [InlineData("--quiet")]
    [InlineData("--log-level Information")]
    [InlineData("--log-level Warning")]
    public void GivenLogOptions_WhenParsing_ThenLogLevelOptionIsPresent(string logArg)
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = $"--sln test.sln --password pass --repository-key repo {logArg}".Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
        // Since we can't easily test the binding here, we at least verify that the options are recognized without errors.
    }
}
