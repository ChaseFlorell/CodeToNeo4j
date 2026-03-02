using System.IO.Abstractions;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public abstract class DocumentHandlerBase(IFileSystem fileSystem) : IDocumentHandler
{
    public int NumberOfFilesHandled => _numberOfFilesHandled;
    public abstract string FileExtension { get; }

    public virtual bool CanHandle(string filePath) => filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase);

    public ValueTask HandleAsync(
        TextDocument? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        string databaseName,
        Accessibility minAccessibility)
    {
        Interlocked.Increment(ref _numberOfFilesHandled);
        return HandleFile(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
    }

    protected abstract ValueTask HandleFile(
        TextDocument? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<Symbol> symbolBuffer,
        ICollection<Relationship> relBuffer,
        string databaseName,
        Accessibility minAccessibility);

    protected async ValueTask<string> GetContent(TextDocument? document, string filePath)
    {
        if (document is not null)
        {
            var sourceText = await document.GetTextAsync().ConfigureAwait(false);
            return sourceText.ToString();
        }

        return await fileSystem.File.ReadAllTextAsync(filePath).ConfigureAwait(false);
    }

    private int _numberOfFilesHandled;
}