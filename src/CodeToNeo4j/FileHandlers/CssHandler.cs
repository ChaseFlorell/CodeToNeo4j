using System.IO.Abstractions;
using System.Text.RegularExpressions;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.FileHandlers;

public partial class CssHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, IConfigurationService configurationService)
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

		ExtractSelectors(content, fileKey, relativePath, fileNamespace ?? string.Empty, symbolBuffer, relBuffer, minAccessibility);

		return new(fileNamespace, fileKey, TargetFrameworks: DetectCssVersion(content));
	}

	internal static IReadOnlySet<string> DetectCssVersion(string content)
	{
		if (Css3FeatureRegex().IsMatch(content))
		{
			return new HashSet<string>(StringComparer.Ordinal) { "css3" };
		}

		return new HashSet<string>(StringComparer.Ordinal) { "css2" };
	}

	[GeneratedRegex(
		@"@keyframes|@supports|@layer|@container|var\(--|:root\b|transition\s*:|transform\s*:|animation\s*:|border-radius\s*:|box-shadow\s*:|flexbox|display\s*:\s*flex|display\s*:\s*grid",
		RegexOptions.IgnoreCase | RegexOptions.Multiline, "en-CA")]
	private static partial Regex Css3FeatureRegex();

	private void ExtractSelectors(string content, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, Accessibility minAccessibility)
	{
		if (!IsPublicAccessible(minAccessibility))
		{
			return;
		}

		// Basic regex to find CSS selectors
		var matches = Regex().Matches(content);

		foreach (Match match in matches)
		{
			var selector = match.Groups[1].Value.Trim();
			if (string.IsNullOrEmpty(selector) || selector.StartsWith("@"))
			{
				continue;
			}

			var startLine = GetLineNumber(content, match.Index);
			var key = textSymbolMapper.BuildKey(fileKey, "CssSelector", selector, startLine);

			var record = textSymbolMapper.CreateSymbol(
				key,
				selector,
				"CssSelector",
				"selector",
				selector,
				fileKey,
				relativePath,
				fileNamespace,
				startLine,
				language: Language);

			symbolBuffer.Add(record);
			relBuffer.Add(new(fileKey, key, "CONTAINS"));
		}
	}

	[GeneratedRegex(@"([^{]+)\s*\{", RegexOptions.Multiline)]
	private static partial Regex Regex();

	private readonly IFileSystem _fileSystem = fileSystem;
}
