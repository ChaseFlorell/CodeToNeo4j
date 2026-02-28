using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class XmlHandler : IDocumentHandler
{
    public bool CanHandle(string filePath) => filePath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    public async ValueTask HandleAsync(
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
        string content;
        if (document is not null)
        {
            var sourceText = await document.GetTextAsync();
            content = sourceText.ToString();
        }
        else
        {
            content = await File.ReadAllTextAsync(filePath);
        }

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

    private void ProcessElement(XElement element, string fileKey, string filePath, ICollection<SymbolRecord> symbolBuffer, ICollection<RelRecord> relBuffer, Accessibility minAccessibility)
    {
        if (Accessibility.Public < minAccessibility) return;

        var name = element.Name.LocalName;
        var lineInfo = (System.Xml.IXmlLineInfo)element;
        var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;

        // Create a key that is somewhat unique
        var key = $"{fileKey}:XmlElement:{name}:{startLine}";

        var record = new SymbolRecord(
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
        relBuffer.Add(new RelRecord(FromKey: fileKey, ToKey: key, RelType: "CONTAINS"));

        // Only process direct children to avoid excessive depth if needed, 
        // but here we'll do full recursion like XamlHandler.
        foreach (var child in element.Elements())
        {
            ProcessElement(child, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        }
    }
}
