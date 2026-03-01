using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public abstract class DocumentHandlerBase : IDocumentHandler
{
    public int NumberOfFilesHandled => _numberOfFilesHandled;
    public abstract string FileType { get; }

    public abstract bool CanHandle(string filePath);

    public virtual async ValueTask HandleAsync(
        Document? document,
        Compilation? compilation,
        string repoKey,
        string fileKey,
        string filePath,
        ICollection<SymbolRecord> symbolBuffer,
        ICollection<RelRecord> relBuffer,
        string databaseName,
        Accessibility minAccessibility)
    {
        Interlocked.Increment(ref _numberOfFilesHandled);
        await Task.CompletedTask;
    }

    protected static async ValueTask<string> GetContent(Document? document, string filePath)
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