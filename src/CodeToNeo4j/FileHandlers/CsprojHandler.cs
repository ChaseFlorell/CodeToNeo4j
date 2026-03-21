using System.IO.Abstractions;
using System.Xml;
using System.Xml.Linq;
using CodeToNeo4j.Configuration;
using CodeToNeo4j.Graph;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.FileHandlers;

public class CsprojHandler(IFileSystem fileSystem, ITextSymbolMapper textSymbolMapper, ILogger<CsprojHandler> logger, IConfigurationService configurationService)
	: PackageDependencyHandlerBase(fileSystem, textSymbolMapper, configurationService)
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
		List<UrlNode> urlNodes = [];

		try
		{
			XDocument xdoc = XDocument.Parse(content, LoadOptions.SetLineInfo);
			if (xdoc.Root != null)
			{
				await ProcessProject(xdoc.Root, fileKey, relativePath, fileNamespace ?? string.Empty, symbolBuffer, relBuffer, urlNodes,
					minAccessibility).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to parse .csproj file: {FilePath}", filePath);
		}

		return new(fileNamespace, fileKey, urlNodes.Count > 0 ? urlNodes : null);
	}

	private async Task ProcessProject(XElement project, string fileKey, string relativePath, string fileNamespace, ICollection<Symbol> symbolBuffer,
		ICollection<Relationship> relBuffer, List<UrlNode> urlNodes, Accessibility minAccessibility)
	{
		if (Accessibility.Public < minAccessibility)
		{
			return;
		}

		// Extract PropertyGroups
		var propertyGroups = project.Elements().Where(e => e.Name.LocalName == "PropertyGroup");
		foreach (var group in propertyGroups)
		{
			var properties = group.Elements();
			foreach (var property in properties)
			{
				var name = property.Name.LocalName;
				var value = property.Value;
				if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(value))
				{
					continue;
				}

				IXmlLineInfo lineInfo = property;
				var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;
				var key = SymbolMapper.BuildKey(fileKey, "Property", name, startLine);

				var record = SymbolMapper.CreateSymbol(
					key,
					name,
					"ProjectProperty",
					name,
					$"{name}: {value}",
					fileKey,
					relativePath,
					fileNamespace,
					startLine,
					documentation: value);

				symbolBuffer.Add(record);
				relBuffer.Add(new(fileKey, key, "HAS_PROPERTY"));
			}
		}

		// Extract PackageReferences
		var packageRefs = project.Descendants().Where(e => e.Name.LocalName == "PackageReference");
		foreach (var packageRef in packageRefs)
		{
			var include = packageRef.Attribute("Include")?.Value;
			var version = packageRef.Attribute("Version")?.Value ?? packageRef.Element(packageRef.Name.Namespace + "Version")?.Value;

			if (string.IsNullOrEmpty(include))
			{
				continue;
			}

			AddDependency(include, version, fileKey, relativePath, fileNamespace, symbolBuffer, relBuffer);
			await CollectNuspecUrls(include, version, urlNodes).ConfigureAwait(false);
		}

		// Extract ProjectReferences
		var projectRefs = project.Descendants()
			.Where(e => e.Name.LocalName == "ProjectReference");
		foreach (var projectRef in projectRefs)
		{
			var include = projectRef.Attribute("Include")?.Value;
			if (string.IsNullOrEmpty(include))
			{
				continue;
			}

			IXmlLineInfo lineInfo = projectRef;
			var startLine = lineInfo.HasLineInfo() ? lineInfo.LineNumber : -1;
			var key = SymbolMapper.BuildKey(fileKey, "ProjectReference", include, startLine);

			var record = SymbolMapper.CreateSymbol(
				key,
				include,
				"ProjectReference",
				include,
				include,
				fileKey,
				relativePath,
				fileNamespace,
				startLine);

			symbolBuffer.Add(record);
			relBuffer.Add(new(fileKey, key, "DEPENDS_ON"));
		}
	}

	private async Task CollectNuspecUrls(string name, string? version, List<UrlNode> urlNodes)
	{
		var (projectUrl, repositoryUrl) = await TryGetNuspecMetadataAsync(name, version).ConfigureAwait(false);
		var depKey = $"pkg:{name}";

		if (!string.IsNullOrEmpty(projectUrl))
		{
			urlNodes.Add(new(depKey, $"url:{projectUrl}", projectUrl));
		}

		if (!string.IsNullOrEmpty(repositoryUrl))
		{
			urlNodes.Add(new(depKey, $"url:{repositoryUrl}", repositoryUrl));
		}
	}

	private async Task<(string? ProjectUrl, string? RepositoryUrl)> TryGetNuspecMetadataAsync(string name, string? version)
	{
		if (string.IsNullOrEmpty(version))
		{
			return (null, null);
		}

		var packagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES")
						   ?? _fileSystem.Path.Combine(
							   Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
							   ".nuget", "packages");

		var nameNormalized = name.ToLowerInvariant();
		var nuspecPath = _fileSystem.Path.Combine(packagesRoot, nameNormalized, version, $"{nameNormalized}.nuspec");

		if (!_fileSystem.File.Exists(nuspecPath))
		{
			return (null, null);
		}

		try
		{
			var nuspecContent = await _fileSystem.File.ReadAllTextAsync(nuspecPath).ConfigureAwait(false);
			XDocument nuspecDoc = XDocument.Parse(nuspecContent);
			var ns = nuspecDoc.Root?.Name.Namespace ?? XNamespace.None;
			var metadata = nuspecDoc.Root?.Element(ns + "metadata");

			var projectUrl = metadata?.Element(ns + "projectUrl")?.Value.Trim();
			if (string.IsNullOrEmpty(projectUrl))
			{
				projectUrl = null;
			}

			var repositoryUrl = metadata?.Element(ns + "repository")?.Attribute("url")?.Value.Trim();
			if (string.IsNullOrEmpty(repositoryUrl))
			{
				repositoryUrl = null;
			}

			return (projectUrl, repositoryUrl);
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Failed to read .nuspec metadata for package: {PackageName}", name);
			return (null, null);
		}
	}

	private readonly IFileSystem _fileSystem = fileSystem;
}
