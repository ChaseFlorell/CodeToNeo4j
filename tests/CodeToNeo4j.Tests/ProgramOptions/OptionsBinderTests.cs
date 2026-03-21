using System.CommandLine;
using System.IO.Abstractions.TestingHelpers;
using CodeToNeo4j.ProgramOptions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.ProgramOptions;

public class OptionsBinderTests
{
	// ── repoKey fallback when --input resolves to a root-like path ────────────

	[Theory]
	[InlineData("/")]
	[InlineData(".")]
	[InlineData(null)]
	public void GivenRootOrRelativeInput_WhenBinding_ThenRepoKeyFallsBackToCurrentDirectoryName(string? inputArg)
	{
		// Arrange — CWD is /home/user/myproject; no .sln/.csproj so auto-detect returns the directory
		MockFileSystem fs = new();
		fs.AddDirectory("/home/user/myproject");
		fs.Directory.SetCurrentDirectory("/home/user/myproject");

		var (binder, root, inputOption) = CreateBinderAndCommand(fs);

		var args = inputArg is null
			? Array.Empty<string>()
			: new[] { "--input", inputArg };

		var parseResult = root.Parse(args);

		// Act
		var options = binder.Bind(parseResult);

		// Assert
		options.RepoKey.ShouldBe("myproject");
	}

	[Fact]
	public void GivenExplicitSlnInput_WhenBinding_ThenRepoKeyDerivedFromSlnName()
	{
		// Arrange
		MockFileSystem fs = new();
		fs.AddFile("/repo/MySolution.sln", new(""));

		var (binder, root, _) = CreateBinderAndCommand(fs);

		var parseResult = root.Parse(["--input", "/repo/MySolution.sln"]);

		// Act
		var options = binder.Bind(parseResult);

		// Assert
		options.RepoKey.ShouldBe("mysolution");
	}

	[Fact]
	public void GivenDirectoryInput_WhenBinding_ThenRepoKeyDerivedFromDirectoryName()
	{
		// Arrange
		MockFileSystem fs = new();
		fs.AddDirectory("/projects/my-app");

		var (binder, root, _) = CreateBinderAndCommand(fs);

		var parseResult = root.Parse(["--input", "/projects/my-app"]);

		// Act
		var options = binder.Bind(parseResult);

		// Assert
		options.RepoKey.ShouldBe("my-app");
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	private static (OptionsBinder binder, RootCommand root, Option<string?> inputOption) CreateBinderAndCommand(
		MockFileSystem fs)
	{
		Option<string?> inputOption = new("--input");
		Option<string> uriOption = new("--uri");
		uriOption.WithDefaultValueFunc(() => "bolt://localhost:7687");
		Option<string> userOption = new("--user");
		userOption.WithDefaultValueFunc(() => "neo4j");
		Option<string> passOption = new("--password");
		passOption.WithDefaultValueFunc(() => "password");
		Option<bool> noKeyOption = new("--no-key");
		Option<string?> diffBaseOption = new("--diff-base");
		Option<int> batchSizeOption = new("--batch-size");
		batchSizeOption.WithDefaultValueFunc(() => 500);
		Option<string> databaseOption = new("--database");
		databaseOption.WithDefaultValueFunc(() => "neo4j");
		Option<Accessibility> minAccessibilityOption = new("--min-accessibility");
		minAccessibilityOption.WithDefaultValueFunc(() => Accessibility.NotApplicable);
		Option<LogLevel> logLevelOption = new("--log-level");
		logLevelOption.WithDefaultValueFunc(() => LogLevel.Information);
		Option<bool> debugOption = new("--debug");
		Option<bool> verboseOption = new("--verbose");
		Option<bool> quietOption = new("--quiet");
		Option<bool> skipDependenciesOption = new("--skip-dependencies");
		Option<bool> purgeDataOption = new("--purge-data");
		Option<string[]> includeExtensionsOption = new("--include");
		includeExtensionsOption.WithDefaultValueFunc(() => []);
		Option<bool> showVersionOption = new("--version");
		Option<bool> showSupportedFilesOption = new("--supported-files");
		Option<bool> showInfoOption = new("--info");

		OptionsBinder binder = new(
			fs,
			new OptionsBinderValidator(),
			inputOption,
			uriOption,
			userOption,
			passOption,
			noKeyOption,
			diffBaseOption,
			batchSizeOption,
			databaseOption,
			minAccessibilityOption,
			logLevelOption,
			debugOption,
			verboseOption,
			quietOption,
			skipDependenciesOption,
			purgeDataOption,
			includeExtensionsOption,
			showVersionOption,
			showSupportedFilesOption,
			showInfoOption);

		RootCommand root = new();
		binder.AddToCommand(root);

		return (binder, root, inputOption);
	}
}
