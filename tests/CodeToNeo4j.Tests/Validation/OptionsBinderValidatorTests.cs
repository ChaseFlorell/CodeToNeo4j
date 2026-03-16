using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Xunit;
using CodeToNeo4j.ProgramOptions;
using Shouldly;

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
    private readonly Option<string> _passOption = new("--password");
    private readonly Option<bool> _showVersionOption = new("--version");
    private readonly Option<bool> _showSupportedFilesOption = new("--supported-files");
    private readonly Option<bool> _showInfoOption = new("--info");

    public OptionsBinderValidatorTests()
    {
        _minAccessibilityOption = new Option<Accessibility>("--min-accessibility");
        _minAccessibilityOption.WithDefaultValueFunc(() => Accessibility.Private);
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
            _minAccessibilityOption,
            _passOption,
            _showVersionOption,
            _showSupportedFilesOption,
            _showInfoOption
        };

        // Remove the built-in VersionOption to avoid conflict with our custom --version flag
        var builtInVersion = root.Options.OfType<VersionOption>().FirstOrDefault();
        if (builtInVersion is not null)
        {
            root.Options.Remove(builtInVersion);
        }

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
            _minAccessibilityOption,
            _passOption,
            _showVersionOption,
            _showSupportedFilesOption,
            _showInfoOption);
    }

    [Fact]
    public void GivenMultipleLogOptions_WhenValidating_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--password", "pass", "--debug", "--quiet");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldHaveSingleItem("Only one of --log-level, --debug, --verbose, or --quiet can be used.");
    }

    [Fact]
    public void GivenPurgeWithSkipDependencies_WhenValidating_ThenShouldNotHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--password", "pass", "--purge-data", "--skip-dependencies");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenPurgeWithMinAccessibility_WhenValidating_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--password", "pass", "--purge-data", "--min-accessibility", "Public");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldHaveSingleItem("--min-accessibility is not allowed when using --purge-data");
    }

    [Fact]
    public void GivenValidOptions_WhenValidating_ThenShouldNotHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln", "--password", "pass", "--debug", "--purge-data");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenNoSln_WhenPurgeDataAndNoKey_ThenShouldNotHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--purge-data", "--no-key", "--password", "pass");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenNoSln_WhenPurgeDataWithoutNoKey_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--purge-data", "--password", "pass");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldHaveSingleItem("--sln is required when using --purge-data without --no-key");
    }

    [Fact]
    public void GivenNoSln_WhenNoPurgeData_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--debug", "--password", "pass");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldHaveSingleItem("--sln is required");
    }

    [Fact]
    public void GivenNoPassword_WhenNoPurgeData_ThenShouldHaveErrorMessage()
    {
        // arrange
        var result = GetCommandResult("--sln", "test.sln");

        // act
        Validate(result);

        // assert
        result.Errors.ShouldHaveSingleItem("--password is required");
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("--supported-files")]
    [InlineData("--info")]
    public void GivenInfoSwitch_WhenNoOtherRequiredOptions_ThenShouldNotHaveErrorMessage(string infoSwitch)
    {
        // arrange
        var result = GetCommandResult(infoSwitch);

        // act
        Validate(result);

        // assert
        result.Errors.ShouldBeEmpty();
    }
}
