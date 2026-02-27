using System.Security.Cryptography;

namespace CodeToNeo4j.Console;

public interface IFileService
{
    string NormalizePath(string path);
    string ComputeSha256(byte[] bytes);
}

public class FileService : IFileService
{
    public string NormalizePath(string path)
    {
        var full = Path.GetFullPath(path);
        return full.Replace('\\', '/');
    }

    public string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
