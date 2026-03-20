using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class HtmlHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, IConfigurationService configurationService)
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

		// Extract script references
		ExtractScriptReferences(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

		// Extract IDs and Classes
		ExtractIdsAndClasses(content, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer, minAccessibility);

		return new(fileNamespace, fileKey);
	}

	private void ExtractScriptReferences(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, Accessibility minAccessibility)
	{
		if (!IsPublicAccessible(minAccessibility))
		{
			return;
		}

		var scriptRegex = ScriptRegex();
		foreach (Match match in scriptRegex.Matches(content))
		{
			var src = match.Groups[1].Value;
			var startLine = GetLineNumber(content, match.Index);
			var key = textSymbolMapper.BuildKey(fileKey, "ScriptRef", src, startLine);

			var record = textSymbolMapper.CreateSymbol(
				key,
				src,
				"HtmlScriptReference",
				"script",
				src,
				fileKey,
				relativePath,
				fileNamespace,
				startLine);

			symbolBuffer.Add(record);
			relBuffer.Add(new(fileKey, key, "DEPENDS_ON"));
		}
	}

	private void ExtractIdsAndClasses(string content, string fileKey, string relativePath, string? fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, Accessibility minAccessibility)
	{
		if (!IsPublicAccessible(minAccessibility))
		{
			return;
		}

		// Extract IDs
		var idRegex = IdRegex();
		foreach (Match match in idRegex.Matches(content))
		{
			var id = match.Groups[1].Value;
			var startLine = GetLineNumber(content, match.Index);
			var key = textSymbolMapper.BuildKey(fileKey, "ElementId", id, startLine);

			var record = textSymbolMapper.CreateSymbol(
				key,
				id,
				"HtmlElementId",
				"element",
				id,
				fileKey,
				relativePath,
				fileNamespace,
				startLine);

			symbolBuffer.Add(record);
			relBuffer.Add(new(fileKey, key, "CONTAINS"));
		}
	}

	[GeneratedRegex(@"id=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-CA")]
	private static partial Regex IdRegex();

	[GeneratedRegex(@"<script\s+.*?src=['""](.*?)['""]", RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-CA")]
	private static partial Regex ScriptRegex();

	private readonly IFileSystem _fileSystem = fileSystem;
}
