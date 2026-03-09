using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using CodeToNeo4j.ProgramOptions;

namespace CodeToNeo4j.Tests.Validation;

public class OptionsBinderValidatorTests
{
    private readonly Option<FileInfo> _slnOption = new("--sln");
    private readonly Option<bool> _noKeyOption = new("--no-key");
    private readonly Option<LogLevel> _logLevelOption = new("--log-level");
    private readonly Option<bool> _debugOption = new("--debug");
    private readonly Option<bool> _verboseOption = new("--verbose");
    private readonly Option<bool> _quietOption = new("--quiet");
    private readonly Option<bool> _purgeDataOption = new("--purge-data");
    private readonly Option<bool> _skipDependenciesOption = new("--skip-dependencies");
    private readonly Option<Accessibility> _minAccessibilityOption;

    public OptionsBinderValidatorTests()
    {
        _minAccessibilityOption = new Option<Accessibility>("--min-accessibility");
        _minAccessibilityOption.SetDefaultValueFactory(() => Accessibility.Private);
    }

    private CommandResult GetCommandResult(params string[] args)
    {
        var root = new RootCommand
        {
            _slnOption,
            _noKeyOption,
            _logLevelOption,
            _debugOption,
            _verboseOption,
            _quietOption,
            _purgeDataOption,
            _skipDependenciesOption,
            _minAccessibilityOption
        };

        return root.Parse(args).CommandResult;
    }

    private void Validate(CommandResult result)
    {
        OptionsBinderValidator.Validate(
            result,
            _slnOption,
            _noKeyOption,
            _logLevelOption,
            _debugOption,
            _verboseOption,
            _quietOption,
            _purgeDataOption,
            _skipDependenciesOption,
            _minAccessibilityOption);
    }

    [Fact]
    public void GivenMultipleLogOptions_WhenValidating_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--debug", "--quiet");

        // act
        Validate(result);

        // assert
        result.ErrorMessage.ShouldBe("Only one of --log-level, --debug, --verbose, or --quiet can be used.");
    }

    [Fact]
    public void GivenPurgeWithSkipDependencies_WhenValidating_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--purge-data", "--skip-dependencies");

        // act
        Validate(result);

        // assert
        result.ErrorMessage.ShouldBe("--skip-dependencies is not allowed when using --purge-data");
    }

    [Fact]
    public void GivenPurgeWithMinAccessibility_WhenValidating_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--purge-data", "--min-accessibility", "Public");

        // act
        Validate(result);

        // assert
        result.ErrorMessage.ShouldBe("--min-accessibility is not allowed when using --purge-data");
    }

    [Fact]
    public void GivenValidOptions_WhenValidating_ThenShouldNotHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--debug", "--purge-data");

        // act
        Validate(result);

        // assert
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void GivenNoSln_WhenPurgeDataAndNoKey_ThenShouldNotHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--purge-data", "--no-key");

        // act
        Validate(result);

        // assert
        result.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public void GivenNoSln_WhenPurgeDataWithoutNoKey_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--purge-data");

        // act
        Validate(result);

        // assert
        result.ErrorMessage.ShouldBe("--sln is required when using --purge-data without --no-key");
    }

    [Fact]
    public void GivenNoSln_WhenNoPurgeData_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--debug");

        // act
        Validate(result);

        // assert
        result.ErrorMessage.ShouldBe("--sln is required");
    }
}
