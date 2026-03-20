namespace CodeToNeo4j.Graph;

public class TextSymbolMapper : ITextSymbolMapper
{
	public string BuildKey(string fileKey, string kindToken, string name, int? startLine = null)
		=> startLine.HasValue
			? $"{fileKey}:{kindToken}:{name}:{startLine.Value}"
			: $"{fileKey}:{kindToken}:{name}";

	public Symbol CreateSymbol(
		string key,
		string name,
		string kind,
		string @class,
		string fqn,
		string fileKey,
		string relativePath,
		string? fileNamespace,
		int startLine,
		string accessibility = "Public",
		string? documentation = null,
		string? version = null,
		string language = "unknown")
		=> new(
			key,
			name,
			kind,
			@class,
			fqn,
			accessibility,
			fileKey,
			relativePath,
			startLine,
			startLine,
			documentation,
			null,
			fileNamespace,
			version,
			language);
}
