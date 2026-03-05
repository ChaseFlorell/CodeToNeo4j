using System.IO.Abstractions;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Completions;

public class ConsoleCompletionsService(
    IFileSystem fileSystem,
    IProcessRunner processRunner,
    IEnvironmentService environmentService,
    ILogger<ConsoleCompletionsService> logger)
    : IConsoleCompletionsService
{
    public async Task EnableCompletions()
    {
        logger.LogInformation("Enabling tab completions for {ToolName}...", ToolName);

        try
        {
            await EnsureDotnetSuggestInstalled();
            var shell = DetectShell();
            await ConfigureShell(shell);

            logger.LogInformation("Tab completions enabled successfully. Please restart your terminal or source your profile to apply changes.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enable tab completions.");
            throw;
        }
    }

    private async Task EnsureDotnetSuggestInstalled()
    {
        logger.LogDebug("Checking if dotnet-suggest is installed...");

        if (await IsToolInstalled("dotnet-suggest"))
        {
            logger.LogDebug("dotnet-suggest is already installed.");
            return;
        }

        logger.LogInformation("Installing dotnet-suggest global tool...");
        await processRunner.RunCommand("dotnet", "tool install --global dotnet-suggest");
    }

    private async Task<bool> IsToolInstalled(string toolName)
    {
        try
        {
            var result = await processRunner.RunCommand("dotnet", "tool list -g");
            return result.Contains(toolName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string DetectShell()
    {
        var shellPath = environmentService.GetEnvironmentVariable("SHELL");
        if (!string.IsNullOrEmpty(shellPath))
        {
            if (shellPath.Contains("zsh")) return "zsh";
            if (shellPath.Contains("bash")) return "bash";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Simple check for PowerShell
            if (!string.IsNullOrEmpty(environmentService.GetEnvironmentVariable("PSModulePath"))) return "pwsh";
        }

        logger.LogWarning("Could not reliably detect shell from SHELL environment variable. Defaulting to 'bash'.");
        return "bash";
    }

    private async Task ConfigureShell(string shell)
    {
        logger.LogInformation("Configuring completions for {Shell}...", shell);

        var shellName = shell switch
        {
            "zsh" => "Zsh",
            "bash" => "Bash",
            "pwsh" => "PowerShell",
            _ => throw new NotSupportedException($"Shell '{shell}' is not supported for auto-configuration.")
        };

        var script = await GetCompletionScript(shellName);

        var profilePath = GetProfilePath(shell);
        if (string.IsNullOrEmpty(profilePath))
        {
            logger.LogWarning("Could not determine profile path for {Shell}. Please add the following to your shell profile manually:\n{Script}", shell, script);
            return;
        }

        await AppendToProfile(profilePath, script);
    }

    private async Task<string> GetCompletionScript(string shellName)
    {
        // 1. Try running dotnet-suggest script <shell> directly
        try
        {
            return await processRunner.RunCommand("dotnet-suggest", $"script {shellName}");
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to run 'dotnet-suggest' directly. Trying fallback.");
        }

        // 2. Try running via dotnet exec fallback (for cases where dotnet-suggest fails with CoreCLR errors or is not in PATH)
        try
        {
            var dllPath = FindDotnetSuggestDll();
            if (!string.IsNullOrEmpty(dllPath))
            {
                return await processRunner.RunCommand("dotnet", $"exec \"{dllPath}\" script {shellName}");
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to run 'dotnet-suggest' via 'dotnet exec' fallback.");
        }

        throw new Exception($"Failed to generate completion script for {shellName}. Ensure 'dotnet-suggest' is installed and working.");
    }

    private string? FindDotnetSuggestDll()
    {
        var home = environmentService.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var toolsStore = fileSystem.Path.Combine(home, ".dotnet");
        toolsStore = fileSystem.Path.Combine(toolsStore, "tools");
        toolsStore = fileSystem.Path.Combine(toolsStore, ".store");
        toolsStore = fileSystem.Path.Combine(toolsStore, "dotnet-suggest");

        if (!fileSystem.Directory.Exists(toolsStore)) return null;

        // Try to find the latest version
        var versions = fileSystem.Directory.GetDirectories(toolsStore);
        foreach (var versionPath in versions.OrderByDescending(v => v))
        {
            var dlls = fileSystem.Directory.GetFiles(versionPath, "dotnet-suggest.dll", SearchOption.AllDirectories);
            var dllPath = dlls.FirstOrDefault();
            if (dllPath != null) return dllPath;
        }

        return null;
    }

    private string? GetProfilePath(string shell)
    {
        var home = environmentService.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return shell switch
        {
            "zsh" => fileSystem.Path.Combine(home, ".zshrc"),
            "bash" => fileSystem.Path.Combine(home, ".bashrc"), // Or .bash_profile on macOS
            "pwsh" => GetPowerShellProfilePath(),
            _ => null
        };
    }

    private string GetPowerShellProfilePath()
    {
        // This is a bit tricky as PowerShell profile path can be found via $PROFILE in PS
        // We'll guess common locations
        var documents = environmentService.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var psPath = fileSystem.Path.Combine(documents, "PowerShell");
        return fileSystem.Path.Combine(psPath, "Microsoft.PowerShell_profile.ps1");
    }

    private async Task AppendToProfile(string path, string script)
    {
        if (!fileSystem.File.Exists(path))
        {
            logger.LogInformation("Creating profile file: {Path}", path);
            await fileSystem.File.WriteAllTextAsync(path, script);
            return;
        }

        var content = await fileSystem.File.ReadAllTextAsync(path);
        if (content.Contains("dotnet-suggest"))
        {
            logger.LogDebug("dotnet-suggest configuration already exists in {Path}.", path);
            return;
        }

        logger.LogInformation("Appending dotnet-suggest configuration to {Path}...", path);
        await fileSystem.File.AppendAllTextAsync(path, "\n" + script + "\n");
    }

    private const string ToolName = "codetoneo4j";
}