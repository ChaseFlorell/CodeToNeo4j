using System.IO.Abstractions;
using System.Xml.Linq;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class XmlHandler(IFileSystem fileSystem) : DocumentHandlerBase(fileSystem)
{
    public override string FileExtension => ".xml";

    protected override async Task<string?> HandleFile(
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
        var content = await GetContent(document, filePath).ConfigureAwait(false);
        var fileNamespace = string.Empty;

        try
        {
            var xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
            if (xdoc.Root == null) return fileNamespace;

            XmlHandler.ProcessElement(xdoc.Root, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        }
        catch (Exception)
        {
            // Fail gracefully for malformed XML
        }

        return fileNamespace;
    }

    private static void ProcessElement(XElement element, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        var name = element.Name.LocalName;
        System.Xml.IXmlLineInfo lineInfo = element;
        var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;

        // Create a key that is somewhat unique
        var key = $"{fileKey}:XmlElement:{name}:{startLine}";

        var record = new Symbol(
            Key: key,
            Name: name,
            Kind: "XmlElement",
            Fqn: name,
            Accessibility: "Public",
            FileKey: fileKey,
            RelativePath: relativePath,
            StartLine: startLine,
            EndLine: startLine,
            Documentation: null,
            Comments: null,
            Namespace: fileNamespace
        );

        symbolBuffer.Add(record);
        relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));

        // Only process direct children to avoid excessive depth if needed, 
        // but here we'll do full recursion like XamlHandler.
        foreach (var child in element.Elements())
        {
            XmlHandler.ProcessElement(child, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
        }
    }
}