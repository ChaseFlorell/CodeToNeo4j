using System.Xml.Linq;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class XmlHandler : DocumentHandlerBase
{
    public override string FileExtension => ".xml";

    public override async ValueTask HandleAsync(
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
        await base.HandleAsync(document, compilation, repoKey, fileKey, filePath, symbolBuffer, relBuffer, databaseName, minAccessibility);
        string content = await GetContent(document, filePath);

        try
        {
            var xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
            if (xdoc.Root == null) return;

            ProcessElement(xdoc.Root, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        }
        catch (Exception)
        {
            // Fail gracefully for malformed XML
        }
    }

    private void ProcessElement(XElement element, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        var name = element.Name.LocalName;
        var lineInfo = (System.Xml.IXmlLineInfo)element;
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
            FilePath: filePath,
            StartLine: startLine,
            EndLine: startLine,
            Documentation: null,
            Comments: null
        );

        symbolBuffer.Add(record);
        relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));

        // Only process direct children to avoid excessive depth if needed, 
        // but here we'll do full recursion like XamlHandler.
        foreach (var child in element.Elements())
        {
            ProcessElement(child, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        }
    }
}
