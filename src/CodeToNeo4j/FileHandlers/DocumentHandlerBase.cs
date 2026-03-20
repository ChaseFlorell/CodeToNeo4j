using System.IO.Abstractions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public abstract class DocumentHandlerBase : IDocumentHandler
{
	public int NumberOfFilesHandled => _numberOfFilesHandled;
	public string FileExtension => Configuration.FileExtension;
	public string Language => Configuration.Language;

	protected HandlerConfiguration Configuration { get; }

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
		=> minAccessibility is <= Accessibility.Public and not Accessibility.NotApplicable;

	protected DocumentHandlerBase(IFileSystem fileSystem, IConfigurationService configurationService)
	{
		this.fileSystem = fileSystem;
		Configuration = configurationService.GetHandlerConfiguration(GetType().Name);
	}

	private readonly IFileSystem fileSystem;
	private int _numberOfFilesHandled;
}
