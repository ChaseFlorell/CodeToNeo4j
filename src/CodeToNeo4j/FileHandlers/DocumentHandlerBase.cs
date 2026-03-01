using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public abstract class DocumentHandlerBase : IDocumentHandler
{
    public int NumberOfFilesHandled => _numberOfFilesHandled;
    public abstract string FileExtension { get; }

    public virtual bool CanHandle(string filePath) => filePath.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase);

    public virtual async ValueTask HandleAsync(
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
        await Task.CompletedTask;
    }

    protected static async ValueTask<string> GetContent(TextDocument? document, string filePath)
    {
        if (document is not null)
        {
            var sourceText = await document.GetTextAsync();
            return sourceText.ToString();
        }

        return await File.ReadAllTextAsync(filePath);
    }

    private int _numberOfFilesHandled;
}