using System.IO.Abstractions;
using System.Security.Cryptography;

namespace CodeToNeo4j.FileSystem;

public class FileService(IFileSystem fileSystem) : IFileService
{
    public string NormalizePath(string path)
    {
        var full = fileSystem.Path.GetFullPath(path);
        return full.Replace('\\', '/');
    }

    public string GetRelativePath(string relativeTo, string path) =>
        fileSystem.Path.GetRelativePath(relativeTo, path).Replace('\\', '/');

    public async Task<string> ComputeSha256(string filePath)
    {
        await using var stream = fileSystem.File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public (string Key, string? Namespace) InferFileMetadata(string relativePath)
    {
        var extension = fileSystem.Path.GetExtension(relativePath);
        var isRoslyn = extension is ".cs" or ".razor" or ".xaml";
        var directory = fileSystem.Path.GetDirectoryName(relativePath) ?? string.Empty;
        var fileNameWithoutExtension = fileSystem.Path.GetFileNameWithoutExtension(relativePath);

        var ns = directory.Replace('\\', '/');

        if (isRoslyn)
        {
            var roslynNs = ns.Replace('/', '.');
            if (roslynNs.StartsWith("src.", StringComparison.OrdinalIgnoreCase)) roslynNs = roslynNs[4..];
            else if (roslynNs.Equals("src", StringComparison.OrdinalIgnoreCase)) roslynNs = string.Empty;
            else if (roslynNs.StartsWith("source.", StringComparison.OrdinalIgnoreCase)) roslynNs = roslynNs[7..];
            else if (roslynNs.Equals("source", StringComparison.OrdinalIgnoreCase)) roslynNs = string.Empty;

            var key = string.IsNullOrEmpty(roslynNs) ? fileNameWithoutExtension : $"{roslynNs}.{fileNameWithoutExtension}";
            return (key, roslynNs);
        }

        return (relativePath, ns);
    }
}