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
        string? version = null)
        => new(
            Key: key,
            Name: name,
            Kind: kind,
            Class: @class,
            Fqn: fqn,
            Accessibility: accessibility,
            FileKey: fileKey,
            RelativePath: relativePath,
            StartLine: startLine,
            EndLine: startLine,
            Documentation: documentation,
            Comments: null,
            Namespace: fileNamespace,
            Version: version);
}
