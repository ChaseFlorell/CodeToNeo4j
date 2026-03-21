using System.IO.Abstractions;
using System.Xml.Linq;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class XmlHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, IXmlAttributeExtractor xmlAttributeExtractor, ILogger<XmlHandler> logger, IConfigurationService configurationService)
	: DocumentHandlerBase(fileSystem, configurationService)
{

	protected override async Task<FileResult> HandleFile(
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
		var fileNamespace = _fileSystem.Path.GetDirectoryName(relativePath)?.Replace('\\', '/');

		try
		{
			XDocument xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
			if (xdoc.Root != null)
			{
				ProcessElement(xdoc.Root, fileKey, relativePath, fileNamespace ?? string.Empty, symbolBuffer, relBuffer, minAccessibility);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to parse XML file: {FilePath}", filePath);
		}

		return new(fileNamespace, fileKey);
	}

	private void ProcessElement(XElement element, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, Accessibility minAccessibility)
	{
		if (Accessibility.Public < minAccessibility)
		{
			return;
		}

		var name = element.Name.LocalName;
		System.Xml.IXmlLineInfo lineInfo = element;
		var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;

		var key = textSymbolMapper.BuildKey(fileKey, "XmlElement", name, startLine);

		var record = textSymbolMapper.CreateSymbol(
			key,
			name,
			"XmlElement",
			"element",
			name,
			fileKey,
			relativePath,
			fileNamespace,
			startLine,
			language: Language, technology: Technology);

		symbolBuffer.Add(record);
		relBuffer.Add(new(fileKey, key, "CONTAINS"));

		xmlAttributeExtractor.ExtractAttributes(
			element, name, key, startLine,
			fileKey, relativePath, fileNamespace,
			textSymbolMapper, symbolBuffer, relBuffer,
			"XmlAttribute", "HAS_ATTRIBUTE",
			skipPredicate: null, commentExtractor: null,
			Language, Technology);

		foreach (var child in element.Elements())
		{
			ProcessElement(child, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);
		}
	}

	private readonly IFileSystem _fileSystem = fileSystem;
}
