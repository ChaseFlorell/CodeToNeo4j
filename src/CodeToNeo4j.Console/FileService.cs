using System.Security.Cryptography;
using System.IO.Abstractions;

namespace CodeToNeo4j.Console;

public interface IFileService
{
    string NormalizePath(string path);
    string ComputeSha256(byte[] bytes);
}

public class FileService(IFileSystem fileSystem) : IFileService
{
    public string NormalizePath(string path)
    {
        var full = fileSystem.Path.GetFullPath(path);
        return full.Replace('\\', '/');
    }

    public string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
