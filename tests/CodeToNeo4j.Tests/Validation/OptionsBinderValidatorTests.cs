using System.CommandLine;
using System.CommandLine.Parsing;
using CodeToNeo4j.ProgramOptions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Validation;

public class OptionsBinderValidatorTests
{
	private static (Func<string[], CommandResult> getResult, Action<CommandResult> validate) CreateTestHarness()
	{
		OptionsBinderValidator sut = new();

		Option<string?> inputOption = new("--input");
		inputOption.WithAlias("--sln");
		inputOption.WithAlias("-s");
		Option<bool> noKeyOption = new("--no-key");
		Option<LogLevel> logLevelOption = new("--log-level");
		Option<bool> debugOption = new("--debug");
		Option<bool> verboseOption = new("--verbose");
		Option<bool> quietOption = new("--quiet");
		Option<bool> purgeDataOption = new("--purge-data");
		Option<bool> skipDependenciesOption = new("--skip-dependencies");
		Option<Accessibility> minAccessibilityOption = new("--min-accessibility");
		minAccessibilityOption.WithDefaultValueFunc(() => Accessibility.Private);
		Option<string> passOption = new("--password");
		Option<bool> showVersionOption = new("--version");
		Option<bool> showSupportedFilesOption = new("--supported-files");
		Option<bool> showInfoOption = new("--info");

		CommandResult GetCommandResult(string[] args)
		{
			RootCommand root =
			[
				inputOption,
				noKeyOption,
				logLevelOption,
				debugOption,
				verboseOption,
				quietOption,
				purgeDataOption,
				skipDependenciesOption,
				minAccessibilityOption,
				passOption,
				showVersionOption,
				showSupportedFilesOption,
				showInfoOption
			];

			var builtInVersion = root.Options.OfType<VersionOption>().FirstOrDefault();
			if (builtInVersion is not null)
			{
				root.Options.Remove(builtInVersion);
			}

			return root.Parse(args).CommandResult;
		}

		void Validate(CommandResult result)
		{
			sut.Validate(
				result,
				inputOption,
				noKeyOption,
				logLevelOption,
				debugOption,
				verboseOption,
				quietOption,
				purgeDataOption,
				skipDependenciesOption,
				minAccessibilityOption,
				passOption,
				showVersionOption,
				showSupportedFilesOption,
				showInfoOption);
		}

		return (GetCommandResult, Validate);
	}

	[Fact]
	public void GivenMultipleLogOptions_WhenValidating_ThenShouldHaveErrorMessage()
	{
		// arrange
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--input", "test.sln", "--password", "pass", "--debug", "--quiet"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldHaveSingleItem("Only one of --log-level, --debug, --verbose, or --quiet can be used.");
	}

	[Fact]
	public void GivenPurgeWithSkipDependencies_WhenValidating_ThenShouldNotHaveErrorMessage()
	{
		// arrange
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--input", "test.sln", "--password", "pass", "--purge-data", "--skip-dependencies"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void GivenPurgeWithMinAccessibility_WhenValidating_ThenShouldHaveErrorMessage()
	{
		// arrange
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--input", "test.sln", "--password", "pass", "--purge-data", "--min-accessibility", "Public"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldHaveSingleItem("--min-accessibility is not allowed when using --purge-data");
	}

	[Fact]
	public void GivenValidOptions_WhenValidating_ThenShouldNotHaveErrorMessage()
	{
		// arrange
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--input", "test.sln", "--password", "pass", "--debug", "--purge-data"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void GivenNoInput_WhenPurgeDataAndNoKey_ThenShouldNotHaveErrorMessage()
	{
		// arrange
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--purge-data", "--no-key", "--password", "pass"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void GivenNoInput_WhenPurgeDataWithoutNoKey_ThenShouldNotHaveErrorMessage()
	{
		// arrange — --input is now optional; auto-detection provides the repo key
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--purge-data", "--password", "pass"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void GivenNoInput_WhenNoPurgeData_ThenShouldNotHaveErrorMessage()
	{
		// arrange — --input is now optional; auto-detection resolves at bind time
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--password", "pass"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void GivenNoPassword_WhenNoPurgeData_ThenShouldHaveErrorMessage()
	{
		// arrange
		var (getResult, validate) = CreateTestHarness();
		var result = getResult(["--input", "test.sln"]);

		// act
		validate(result);

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
		var (getResult, validate) = CreateTestHarness();
		var result = getResult([infoSwitch]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldBeEmpty();
	}

	[Theory]
	[InlineData("--sln")]
	[InlineData("-s")]
	public void GivenAlias_WhenUsedInsteadOfInput_ThenShouldNotHaveErrorMessage(string alias)
	{
		// arrange
		var (getResult, validate) = CreateTestHarness();
		var result = getResult([alias, "test.sln", "--password", "pass"]);

		// act
		validate(result);

		// assert
		result.Errors.ShouldBeEmpty();
	}
}
