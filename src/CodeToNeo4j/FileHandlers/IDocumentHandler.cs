using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public interface IDocumentHandler
{
	bool CanHandle(string filePath);

	Task<FileResult> Handle(
		TextDocument? document,
		Compilation? compilation,
		string? repoKey,
		string fileKey,
		string filePath,
		string relativePath,
		ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer,
		Accessibility minAccessibility);

	int NumberOfFilesHandled { get; }
	string FileExtension { get; }
}
