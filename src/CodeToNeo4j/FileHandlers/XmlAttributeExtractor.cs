using System.Xml.Linq;
using CodeToNeo4j.Graph;

namespace CodeToNeo4j.FileHandlers;

internal static class XmlAttributeExtractor
{
	internal static void ExtractAttributes(
		XElement element,
		string elementName,
		string parentKey,
		int startLine,
		string fileKey,
		string relativePath,
		string? fileNamespace,
		ITextSymbolMapper textSymbolMapper,
		ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer,
		string kindToken,
		string relType,
		Func<XAttribute, bool>? skipPredicate,
		Func<string, string?>? commentExtractor,
		string language,
		string technology)
	{
		foreach (var attr in element.Attributes())
		{
			if (skipPredicate != null && skipPredicate(attr))
			{
				continue;
			}

			var attrName = attr.Name.LocalName;
			var attrValue = attr.Value;
			var attrKey = textSymbolMapper.BuildKey(fileKey, kindToken, $"{elementName}.{attrName}", startLine);

			var attrRecord = textSymbolMapper.CreateSymbol(
				attrKey,
				attrName,
				kindToken,
				"attribute",
				$"{elementName}.{attrName}={attrValue}",
				fileKey,
				relativePath,
				fileNamespace,
				startLine,
				documentation: attrValue,
				comments: commentExtractor?.Invoke(attrValue),
				language: language, technology: technology);

			symbolBuffer.Add(attrRecord);
			relBuffer.Add(new(parentKey, attrKey, relType));
		}
	}
}
