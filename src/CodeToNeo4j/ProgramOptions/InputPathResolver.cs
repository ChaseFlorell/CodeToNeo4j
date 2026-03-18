using System.IO.Abstractions;

namespace CodeToNeo4j.ProgramOptions;

public class InputPathResolver(IFileSystem fileSystem)
{
    private static readonly string[] SupportedExtensions = [".sln", ".slnx", ".csproj"];

    public string Resolve(string? explicitPath) =>
        explicitPath is not null
            ? ResolveExplicit(explicitPath)
            : AutoDetect(fileSystem.Directory.GetCurrentDirectory());

    internal string ResolveExplicit(string path)
    {
        var fullPath = fileSystem.Path.GetFullPath(path);

        if (fileSystem.Directory.Exists(fullPath))
        {
            return fullPath;
        }

        if (!fileSystem.File.Exists(fullPath))
        {
            throw new InvalidOperationException($"Path does not exist: {path}");
        }

        var ext = fileSystem.Path.GetExtension(fullPath);
        if (!SupportedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported file type '{ext}'. Supported extensions: {string.Join(", ", SupportedExtensions)}");
        }

        return fullPath;
    }

    internal string AutoDetect(string directory)
    {
        var candidates = FindCandidates(directory, "*.sln");
        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple .sln files found in current directory. Specify --input explicitly.");
        }

        candidates = FindCandidates(directory, "*.slnx");
        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple .slnx files found in current directory. Specify --input explicitly.");
        }

        candidates = FindCandidates(directory, "*.csproj");
        if (candidates.Length == 1)
        {
            return candidates[0];
        }

        if (candidates.Length > 1)
        {
            throw new InvalidOperationException(
                "Multiple .csproj files found in current directory. Specify --input explicitly.");
        }

        if (fileSystem.File.Exists(fileSystem.Path.Combine(directory, "pubspec.yaml")))
        {
            return directory;
        }

        // Files-only mode
        return directory;
    }

    private string[] FindCandidates(string directory, string pattern)
    {
        var extension = pattern.Replace("*", "");
        return fileSystem.Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .Where(f => fileSystem.Path.GetExtension(f).Equals(extension, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }
}
