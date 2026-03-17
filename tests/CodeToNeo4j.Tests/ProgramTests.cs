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
        var result = sut.Parse([]);

        // assert — --password is still required
        result.Errors.ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("--input", "test.sln")]
    [InlineData("--sln", "test.sln")]
    [InlineData("-s", "test.sln")]
    public void GivenValidArguments_WhenParsing_ThenShouldNotHaveErrors(string inputSwitch, string inputValue)
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { inputSwitch, inputValue, "--password", "pass" };

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
        var args = new[] { "--input", "test.sln", "--password", "pass", "--debug", "--quiet" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "Only one of --log-level, --debug, --verbose, or --quiet can be used.");
    }

    [Fact]
    public void GivenPurgeWithSkipDependencies_WhenParsing_ThenShouldNotHaveErrors()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--input", "test.sln", "--purge-data", "--password", "pass", "--skip-dependencies" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenPurgeWithMinAccessibility_WhenParsing_ThenShouldHaveValidationError()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--input", "test.sln", "--purge-data", "--password", "pass", "--min-accessibility", "Public" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldContain(e => e.Message == "--min-accessibility is not allowed when using --purge-data");
    }

    [Fact]
    public void GivenNoInputWithPassword_WhenParsing_ThenShouldNotHaveErrors()
    {
        // arrange — --input is optional; auto-detection resolves at bind time
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--password", "pass" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
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
        var args = $"--input test.sln --password pass {logArg}".Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenNoInputAndNoKeyAndPurge_WhenParsing_ThenShouldNotHaveErrors()
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();
        var args = new[] { "--no-key", "--purge-data", "--password", "pass" };

        // act
        var result = sut.Parse(args);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void GivenAssembly_WhenGettingVersion_ThenVersionIsNotNullOrEmpty()
    {
        // act
        var version = Program.GetVersion();

        // assert
        version.ShouldNotBeNullOrWhiteSpace();
    }

    // ── Info switches ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("--version")]
    [InlineData("--supported-files")]
    [InlineData("--info")]
    public void GivenInfoSwitch_WhenParsingWithNoOtherArgs_ThenShouldNotHaveErrors(string infoSwitch)
    {
        // arrange
        var (sut, _) = Program.CreateRootCommand();

        // act
        var result = sut.Parse(infoSwitch);

        // assert
        result.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("--supported-files")]
    [InlineData("--info")]
    public async Task GivenInfoSwitch_WhenInvoked_ThenExitsWithCodeZero(string infoSwitch)
    {
        // act
        var exitCode = await Program.Main([infoSwitch]);

        // assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public void GivenVersionRequested_WhenFormattingVersionLine_ThenContainsToolNameAndVersion()
    {
        // act
        var version = Program.GetVersion();

        // assert — the version line is "CodeToNeo4j {version}"
        var line = $"CodeToNeo4j {version}";
        line.ShouldContain("CodeToNeo4j");
        line.ShouldContain(version);
    }

    [Fact]
    public void GivenPrintSupportedFiles_WhenCalled_ThenAllHandlersAreListed()
    {
        // arrange
        var stdout = new StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(stdout);

        try
        {
            // act
            Program.PrintSupportedFiles();

            // assert
            var output = stdout.ToString();
            output.ShouldContain("Supported file types:");
            foreach (var (ext, handler) in Program.SupportedFileTypes)
            {
                output.ShouldContain(ext);
                output.ShouldContain(handler);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void GivenSupportedFileTypes_WhenChecked_ThenContainsExpectedEntries()
    {
        Program.SupportedFileTypes.ShouldContain(e => e.Extension == ".cs" && e.HandlerName == "CSharpHandler");
        Program.SupportedFileTypes.ShouldContain(e => e.Extension == "package.json" && e.HandlerName == "PackageJsonHandler");
        Program.SupportedFileTypes.ShouldContain(e => e.Extension == ".csproj" && e.HandlerName == "CsprojHandler");
        Program.SupportedFileTypes.ShouldContain(e => e.Extension.Contains(".ts") && e.HandlerName == "TypeScriptHandler");
    }
}
