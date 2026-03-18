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
        var fs = new MockFileSystem();
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
        var fs = new MockFileSystem();
        fs.AddFile("/repo/MySolution.sln", new MockFileData(""));

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
        var fs = new MockFileSystem();
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
        var inputOption = new Option<string?>("--input");
        var uriOption = new Option<string>("--uri");
        uriOption.WithDefaultValueFunc(() => "bolt://localhost:7687");
        var userOption = new Option<string>("--user");
        userOption.WithDefaultValueFunc(() => "neo4j");
        var passOption = new Option<string>("--password");
        passOption.WithDefaultValueFunc(() => "password");
        var noKeyOption = new Option<bool>("--no-key");
        var diffBaseOption = new Option<string?>("--diff-base");
        var batchSizeOption = new Option<int>("--batch-size");
        batchSizeOption.WithDefaultValueFunc(() => 500);
        var databaseOption = new Option<string>("--database");
        databaseOption.WithDefaultValueFunc(() => "neo4j");
        var minAccessibilityOption = new Option<Accessibility>("--min-accessibility");
        minAccessibilityOption.WithDefaultValueFunc(() => Accessibility.NotApplicable);
        var logLevelOption = new Option<LogLevel>("--log-level");
        logLevelOption.WithDefaultValueFunc(() => LogLevel.Information);
        var debugOption = new Option<bool>("--debug");
        var verboseOption = new Option<bool>("--verbose");
        var quietOption = new Option<bool>("--quiet");
        var skipDependenciesOption = new Option<bool>("--skip-dependencies");
        var purgeDataOption = new Option<bool>("--purge-data");
        var includeExtensionsOption = new Option<string[]>("--include");
        includeExtensionsOption.WithDefaultValueFunc(() => []);
        var showVersionOption = new Option<bool>("--version");
        var showSupportedFilesOption = new Option<bool>("--supported-files");
        var showInfoOption = new Option<bool>("--info");

        var binder = new OptionsBinder(
            fs,
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

        var root = new RootCommand();
        binder.AddToCommand(root);

        return (binder, root, inputOption);
    }
}
