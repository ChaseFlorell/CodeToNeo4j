using System.IO.Abstractions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public abstract class DocumentHandlerBase(IFileSystem fileSystem) : IDocumentHandler
{
    public int NumberOfFilesHandled => _numberOfFilesHandled;
    public abstract string FileExtension { get; }

    public virtual bool CanHandle(string filePath) => filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase);

    public Task<FileResult> Handle(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        string relativePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        Interlocked.Increment(ref _numberOfFilesHandled);
        return HandleFile(document, compilation, repoKey, fileKey, filePath, relativePath, symbolBuffer, relBuffer, minAccessibility);
    }

    protected abstract Task<FileResult> HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        string relativePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility);

    protected Task<string> GetContent(TextDocument? document, string filePath) =>
        document is not null
            ? document.GetTextAsync()
                .ContinueWith(x => x.Result.ToString())
            : fileSystem.File.ReadAllTextAsync(filePath);

    protected static int GetLineNumber(string content, int index)
        => content[..index].Count(c => c == '\n') + 1;

    protected static bool IsPublicAccessible(Accessibility minAccessibility)
        => Accessibility.Public >= minAccessibility;

    private int _numberOfFilesHandled;
}