using System.IO.Abstractions;
using CodeToNeo4j.Completions;
using FakeItEasy;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace CodeToNeo4j.Tests.Completions;

public class ConsoleCompletionsServiceTests
{
    [Fact]
    public async Task GivenZshDetected_WhenEnableCompletionsCalled_ThenConfiguresZshProfile()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var processRunner = A.Fake<IProcessRunner>();
        var environmentService = A.Fake<IEnvironmentService>();
        var logger = A.Fake<ILogger<ConsoleCompletionsService>>();
        var sut = new ConsoleCompletionsService(fileSystem, processRunner, environmentService, logger);

        A.CallTo(() => environmentService.GetEnvironmentVariable("SHELL")).Returns("/bin/zsh");
        A.CallTo(() => environmentService.GetFolderPath(Environment.SpecialFolder.UserProfile)).Returns("/home/user");
        A.CallTo(() => processRunner.RunCommand("dotnet", "tool list -g")).Returns("dotnet-suggest 2.0.3");
        A.CallTo(() => processRunner.RunCommand("dotnet-suggest", "script Zsh")).Returns("zsh completion script");
        
        var profilePath = "/home/user/.zshrc";
        A.CallTo(() => fileSystem.File.Exists(profilePath)).Returns(true);
        A.CallTo(() => fileSystem.File.ReadAllTextAsync(profilePath, default)).Returns("existing content");
        A.CallTo(() => fileSystem.Path.Combine("/home/user", ".zshrc")).Returns(profilePath);

        // Act
        await sut.EnableCompletions();

        // Assert
        A.CallTo(() => fileSystem.File.AppendAllTextAsync(profilePath, A<string>.That.Contains("zsh completion script"), default))
            .MustHaveHappened();
    }

    [Fact]
    public async Task GivenBashDetected_WhenEnableCompletionsCalled_ThenConfiguresBashProfile()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var processRunner = A.Fake<IProcessRunner>();
        var environmentService = A.Fake<IEnvironmentService>();
        var logger = A.Fake<ILogger<ConsoleCompletionsService>>();
        var sut = new ConsoleCompletionsService(fileSystem, processRunner, environmentService, logger);

        A.CallTo(() => environmentService.GetEnvironmentVariable("SHELL")).Returns("/bin/bash");
        A.CallTo(() => environmentService.GetFolderPath(Environment.SpecialFolder.UserProfile)).Returns("/home/user");
        A.CallTo(() => processRunner.RunCommand("dotnet", "tool list -g")).Returns("dotnet-suggest 2.0.3");
        A.CallTo(() => processRunner.RunCommand("dotnet-suggest", "script Bash")).Returns("bash completion script");
        
        var profilePath = "/home/user/.bashrc";
        A.CallTo(() => fileSystem.File.Exists(profilePath)).Returns(true);
        A.CallTo(() => fileSystem.File.ReadAllTextAsync(profilePath, default)).Returns("existing content");
        A.CallTo(() => fileSystem.Path.Combine("/home/user", ".bashrc")).Returns(profilePath);

        // Act
        await sut.EnableCompletions();

        // Assert
        A.CallTo(() => fileSystem.File.AppendAllTextAsync(profilePath, A<string>.That.Contains("bash completion script"), default))
            .MustHaveHappened();
    }

    [Fact]
    public async Task GivenToolNotInstalled_WhenEnableCompletionsCalled_ThenInstallsToolFirst()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var processRunner = A.Fake<IProcessRunner>();
        var environmentService = A.Fake<IEnvironmentService>();
        var logger = A.Fake<ILogger<ConsoleCompletionsService>>();
        var sut = new ConsoleCompletionsService(fileSystem, processRunner, environmentService, logger);

        A.CallTo(() => environmentService.GetEnvironmentVariable("SHELL")).Returns("/bin/zsh");
        A.CallTo(() => processRunner.RunCommand("dotnet", "tool list -g")).Returns("other-tool 1.0.0");
        
        // Act
        await sut.EnableCompletions();

        // Assert
        A.CallTo(() => processRunner.RunCommand("dotnet", "tool install --global dotnet-suggest"))
            .MustHaveHappened();
    }

    [Fact]
    public async Task GivenProfileContainsDotnetSuggest_WhenEnableCompletionsCalled_ThenDoesNotAppendAgain()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var processRunner = A.Fake<IProcessRunner>();
        var environmentService = A.Fake<IEnvironmentService>();
        var logger = A.Fake<ILogger<ConsoleCompletionsService>>();
        var sut = new ConsoleCompletionsService(fileSystem, processRunner, environmentService, logger);

        A.CallTo(() => environmentService.GetEnvironmentVariable("SHELL")).Returns("/bin/zsh");
        A.CallTo(() => environmentService.GetFolderPath(Environment.SpecialFolder.UserProfile)).Returns("/home/user");
        A.CallTo(() => processRunner.RunCommand("dotnet", "tool list -g")).Returns("dotnet-suggest 2.0.3");
        A.CallTo(() => processRunner.RunCommand("dotnet-suggest", "script Zsh")).Returns("zsh completion script");
        
        var profilePath = "/home/user/.zshrc";
        A.CallTo(() => fileSystem.File.Exists(profilePath)).Returns(true);
        A.CallTo(() => fileSystem.File.ReadAllTextAsync(profilePath, default)).Returns("content with dotnet-suggest script");
        A.CallTo(() => fileSystem.Path.Combine("/home/user", ".zshrc")).Returns(profilePath);

        // Act
        await sut.EnableCompletions();

        // Assert
        A.CallTo(() => fileSystem.File.AppendAllTextAsync(A<string>._, A<string>._, default))
            .MustNotHaveHappened();
    }

    [Fact]
    public async Task GivenDirectCommandFails_WhenEnableCompletionsCalled_ThenUsesFallback()
    {
        // Arrange
        var fileSystem = A.Fake<IFileSystem>();
        var processRunner = A.Fake<IProcessRunner>();
        var environmentService = A.Fake<IEnvironmentService>();
        var logger = A.Fake<ILogger<ConsoleCompletionsService>>();
        var sut = new ConsoleCompletionsService(fileSystem, processRunner, environmentService, logger);

        A.CallTo(() => environmentService.GetEnvironmentVariable("SHELL")).Returns("/bin/zsh");
        A.CallTo(() => environmentService.GetFolderPath(Environment.SpecialFolder.UserProfile)).Returns("/home/user");
        A.CallTo(() => processRunner.RunCommand("dotnet", "tool list -g")).Returns("dotnet-suggest 2.0.3");
        
        // Fail direct call
        A.CallTo(() => processRunner.RunCommand("dotnet-suggest", "script Zsh")).Throws(new Exception("Fail"));

        // Setup fallback
        var toolsStore = "/home/user/.dotnet/tools/.store/dotnet-suggest";
        var versionPath = toolsStore + "/2.0.3";
        var dllPath = versionPath + "/tools/net8.0/any/dotnet-suggest.dll";
        
        A.CallTo(() => fileSystem.Path.Combine(A<string>._, A<string>._))
            .ReturnsLazily(call => (string)call.Arguments[0]! + "/" + (string)call.Arguments[1]!);
        A.CallTo(() => fileSystem.Directory.Exists(toolsStore)).Returns(true);
        A.CallTo(() => fileSystem.Directory.GetDirectories(toolsStore)).Returns(new[] { versionPath });
        A.CallTo(() => fileSystem.Directory.GetFiles(versionPath, "dotnet-suggest.dll", SearchOption.AllDirectories)).Returns(new[] { dllPath });

        A.CallTo(() => processRunner.RunCommand("dotnet", $"exec \"{dllPath}\" script Zsh")).Returns("fallback completion script");
        
        var profilePath = "/home/user/.zshrc";
        A.CallTo(() => fileSystem.File.Exists(profilePath)).Returns(true);
        A.CallTo(() => fileSystem.File.ReadAllTextAsync(profilePath, default)).Returns("existing content");
        A.CallTo(() => fileSystem.Path.Combine("/home/user", ".zshrc")).Returns(profilePath);

        // Act
        await sut.EnableCompletions();

        // Assert
        A.CallTo(() => fileSystem.File.AppendAllTextAsync(profilePath, A<string>.That.Contains("fallback completion script"), default))
            .MustHaveHappened();
    }
}
