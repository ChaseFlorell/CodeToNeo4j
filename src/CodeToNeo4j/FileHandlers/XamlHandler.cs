using System.Xml.Linq;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public class XamlHandler : DocumentHandlerBase
{
    public override string FileExtension => ".xaml";

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

            ProcessElement(xdoc.Root, repoKey, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        }
        catch (Exception)
        {
            // Logging would be good here, but for now we'll just fail gracefully
        }
    }

    private void ProcessElement(XElement element, string repoKey, string fileKey, string filePath, ICollection<Symbol> symbolBuffer, ICollection<Relationship> relBuffer, Accessibility minAccessibility)
    {
        var name = element.Name.LocalName;
        var keySuffix = "";

        // Look for x:Key or x:Name
        var xNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
        var xNameAttr = element.Attribute(XName.Get("Name", xNamespace)) ?? element.Attribute("Name");
        var xKeyAttr = element.Attribute(XName.Get("Key", xNamespace));

        if (xNameAttr != null) keySuffix = $":{xNameAttr.Value}";
        else if (xKeyAttr != null) keySuffix = $":{xKeyAttr.Value}";

        var lineInfo = (System.Xml.IXmlLineInfo)element;
        var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;

        var symbolKey = $"{fileKey}:{name}{keySuffix}:{startLine}";
        if (Accessibility.Public >= minAccessibility)
        {
            var record = new Symbol(
                Key: symbolKey,
                Name: xNameAttr?.Value ?? xKeyAttr?.Value ?? name,
                Kind: "XamlElement",
                Fqn: $"{name}{keySuffix}",
                Accessibility: "Public",
                FileKey: fileKey,
                FilePath: filePath,
                StartLine: startLine,
                EndLine: startLine,
                Documentation: null,
                Comments: null
            );

            symbolBuffer.Add(record);
            relBuffer.Add(new Relationship(FromKey: fileKey, ToKey: symbolKey, RelType: "CONTAINS"));
        }

        // Extract potential event handlers
        foreach (var attr in element.Attributes())
        {
            if (IsEventHandler(attr.Name.LocalName))
            {
                if (Accessibility.Private >= minAccessibility)
                {
                    var handlerKey = $"{fileKey}:EventHandler:{attr.Value}";
                    var handlerRecord = new Symbol(
                        Key: handlerKey,
                        Name: attr.Value,
                        Kind: "XamlEventHandler",
                        Fqn: attr.Value,
                        Accessibility: "Private",
                        FileKey: fileKey,
                        FilePath: filePath,
                        StartLine: startLine,
                        EndLine: startLine,
                        Documentation: null,
                        Comments: null
                    );
                    symbolBuffer.Add(handlerRecord);
                    relBuffer.Add(new Relationship(FromKey: symbolKey, ToKey: handlerKey, RelType: "BINDS_TO"));
                }
            }
        }

        foreach (var child in element.Elements())
        {
            ProcessElement(child, repoKey, fileKey, filePath, symbolBuffer, relBuffer, minAccessibility);
        }
    }

    private bool IsEventHandler(string attrName)
    {
        // Common event naming patterns in XAML
        return attrName.EndsWith("Click") || 
               attrName.EndsWith("Changed") || 
               attrName.EndsWith("Loaded") || 
               attrName.EndsWith("Pressed") || 
               attrName.EndsWith("Released") ||
               attrName == "Command";
    }
}
