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
}