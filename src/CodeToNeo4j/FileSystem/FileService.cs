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

    public string GetRelativePath(string relativeTo, string path)
    {
        return fileSystem.Path.GetRelativePath(relativeTo, path).Replace('\\', '/');
    }

    public string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<string> ComputeSha256(string filePath)
    {
        using var stream = fileSystem.File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
