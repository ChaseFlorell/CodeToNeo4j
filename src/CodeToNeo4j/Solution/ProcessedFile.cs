using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Solution;

public record ProcessedFile(
	string FilePath,
	ProjectId? ProjectId = null,
	DocumentId? DocumentId = null);
