using System.Diagnostics;

namespace CodeToNeo4j.Completions;

public class ProcessRunner : IProcessRunner
{
    public async Task<string> RunCommand(string command, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null) throw new Exception($"Failed to start process: {command}");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new Exception($"Command '{command} {arguments}' failed with exit code {process.ExitCode}. Error: {error}");
        }

        return output;
    }
}
