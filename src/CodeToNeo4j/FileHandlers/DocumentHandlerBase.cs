using System.IO.Abstractions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public abstract class DocumentHandlerBase(IFileSystem fileSystem) : IDocumentHandler
{
    public int NumberOfFilesHandled => _numberOfFilesHandled;
    public abstract string FileExtension { get; }

    public virtual bool CanHandle(string filePath) => filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase);

    public Task Handle(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility)
    {
        Interlocked.Increment(ref _numberOfFilesHandled);
        return HandleFile(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
    }

    protected abstract Task HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string? repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        Accessibility minAccessibility);

    protected async Task<string> GetContent(TextDocument? document, string filePath) =>
        document is not null
            ? await document.GetTextAsync().ContinueWith(x => x.Result.ToString()).ConfigureAwait(false)
            : await fileSystem.File.ReadAllTextAsync(filePath).ConfigureAwait(false);

    private int _numberOfFilesHandled;
}