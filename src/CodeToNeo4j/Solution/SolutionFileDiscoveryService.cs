using System.IO.Abstractions;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CodeToNeo4j.FileSystem;

namespace CodeToNeo4j.Solution;

public partial class SolutionFileDiscoveryService(
	IFileService fileService,
	IFileSystem fileSystem) : ISolutionFileDiscoveryService
{
	public IEnumerable<ProcessedFile> GetFilesToProcess(string rootDirectory,
		Microsoft.CodeAnalysis.Solution? solution,
		IEnumerable<string> includeExtensions)
	{
		HashSet<string> extensions = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
		// Track files with their first-seen ProjectId/DocumentId and a mutable TFM set to avoid
		// allocation churn from copy-on-merge with immutable sets.
		Dictionary<string, (ProcessedFile File, HashSet<string>? Tfms)> solutionFiles = new(StringComparer.OrdinalIgnoreCase);

		// 1. Get all documents from MSBuild (when a solution/project is loaded)
		if (solution is not null)
		{
			// Cache TFMs by project file path so each .csproj is read at most once
			Dictionary<string, IReadOnlySet<string>?> projectFileTfmCache = new(StringComparer.OrdinalIgnoreCase);

			foreach (var project in solution.Projects)
			{
				// Skip outer multi-target wrapper projects (they have 0 documents and no useful data)
				if (!project.Documents.Any() && !project.AdditionalDocuments.Any())
				{
					continue;
				}

				// Fast path: TFM embedded in project name (e.g. "MyApp (net9.0)") — set by MSBuild for
				// each target-framework instance of a multi-targeted project.
				var tfmFromName = ExtractTargetFramework(project.Name);
				IReadOnlySet<string>? projectTfms = tfmFromName is not null
					? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { tfmFromName }
					: ReadProjectFileTfms(project.FilePath, projectFileTfmCache);

				// Regular Documents
				foreach (var doc in project.Documents)
				{
					var path = fileService.NormalizePath(doc.FilePath!);
					if (string.IsNullOrEmpty(path))
					{
						continue;
					}

					if (!extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
					{
						continue;
					}

					if (solutionFiles.TryGetValue(path, out var existing))
					{
						if (projectTfms is not null)
						{
							existing.Tfms ??= new(StringComparer.OrdinalIgnoreCase);
							foreach (var t in projectTfms)
							{
								existing.Tfms.Add(t);
							}
						}
					}
					else
					{
						HashSet<string>? tfms = projectTfms is not null
							? new HashSet<string>(projectTfms, StringComparer.OrdinalIgnoreCase)
							: null;
						solutionFiles[path] = (new(path, project.Id, doc.Id), tfms);
					}
				}

				// Additional Documents
				foreach (var doc in project.AdditionalDocuments)
				{
					var path = fileService.NormalizePath(doc.FilePath!);
					if (string.IsNullOrEmpty(path))
					{
						continue;
					}

					if (!extensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
					{
						continue;
					}

					if (solutionFiles.TryGetValue(path, out var existing))
					{
						if (projectTfms is not null)
						{
							existing.Tfms ??= new(StringComparer.OrdinalIgnoreCase);
							foreach (var t in projectTfms)
							{
								existing.Tfms.Add(t);
							}
						}
					}
					else
					{
						HashSet<string>? tfms = projectTfms is not null
							? new HashSet<string>(projectTfms, StringComparer.OrdinalIgnoreCase)
							: null;
						solutionFiles[path] = (new(path, project.Id, doc.Id), tfms);
					}
				}
			}
		}

		// 2. File system fallback for other files in the directory
		var allFilesOnDisk = fileSystem.Directory.EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories);
		foreach (var fileOnDisk in allFilesOnDisk)
		{
			var normalizedPath = fileService.NormalizePath(fileOnDisk);
			if (IsExcluded(normalizedPath))
			{
				continue;
			}

			if (!extensions.Any(ext => normalizedPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
			{
				continue;
			}

			if (!solutionFiles.ContainsKey(normalizedPath))
			{
				solutionFiles[normalizedPath] = (new(normalizedPath, null, null), null);
			}
		}

		// Freeze the mutable TFM sets into the ProcessedFile records
		return solutionFiles.Values.Select(entry =>
			entry.Tfms is { Count: > 0 }
				? entry.File with { TargetFrameworks = entry.Tfms }
				: entry.File);
	}

	public IEnumerable<ProcessedFile> GetFilesToProcess(string directoryPath,
		IEnumerable<string> includeExtensions)
	{
		HashSet<string> extensions = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
		Dictionary<string, ProcessedFile> files = new(StringComparer.OrdinalIgnoreCase);

		var allFilesOnDisk = fileSystem.Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories);
		foreach (var fileOnDisk in allFilesOnDisk)
		{
			var normalizedPath = fileService.NormalizePath(fileOnDisk);
			if (IsExcluded(normalizedPath))
			{
				continue;
			}

			var fileName = Path.GetFileName(normalizedPath);
			var isFullNameMatch = extensions.Contains(fileName);
			var isExtensionMatch = extensions.Any(ext => ext.StartsWith('.') && normalizedPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));

			if (!isFullNameMatch && !isExtensionMatch)
			{
				continue;
			}

			if (!files.ContainsKey(normalizedPath))
			{
				files[normalizedPath] = new(normalizedPath, null, null);
			}
		}

		// Also include pubspec.yaml if it matches
		var pubspecPath = fileService.NormalizePath(Path.Combine(directoryPath, "pubspec.yaml"));
		if (fileSystem.File.Exists(pubspecPath) && !files.ContainsKey(pubspecPath))
		{
			files[pubspecPath] = new(pubspecPath, null, null);
		}

		return files.Values;
	}

	private IReadOnlySet<string>? ReadProjectFileTfms(string? projectFilePath, Dictionary<string, IReadOnlySet<string>?> cache)
	{
		if (string.IsNullOrEmpty(projectFilePath))
		{
			return null;
		}

		if (cache.TryGetValue(projectFilePath, out var cached))
		{
			return cached;
		}

		IReadOnlySet<string>? result = null;

		try
		{
			if (fileSystem.File.Exists(projectFilePath))
			{
				string content = fileSystem.File.ReadAllText(projectFilePath);
				XDocument xdoc = XDocument.Parse(content);
				if (xdoc.Root is not null)
				{
					HashSet<string> tfms = new(StringComparer.OrdinalIgnoreCase);
					foreach (var pg in xdoc.Root.Elements().Where(e => e.Name.LocalName == "PropertyGroup"))
					{
						var single = pg.Elements().FirstOrDefault(e => e.Name.LocalName == "TargetFramework")?.Value.Trim();
						if (!string.IsNullOrEmpty(single))
						{
							tfms.Add(single);
						}

						var multi = pg.Elements().FirstOrDefault(e => e.Name.LocalName == "TargetFrameworks")?.Value.Trim();
						if (!string.IsNullOrEmpty(multi))
						{
							foreach (var tfm in multi.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
							{
								tfms.Add(tfm);
							}
						}
					}

					result = tfms.Count > 0 ? tfms : null;
				}
			}
		}
		catch
		{
			// ignore malformed project files
		}

		cache[projectFilePath] = result;
		return result;
	}

	internal static string? ExtractTargetFramework(string projectName)
	{
		var match = TfmRegex().Match(projectName);
		return match.Success ? match.Groups[1].Value : null;
	}

	private static bool IsExcluded(string path) =>
		path.Split('/')
			.Any(p => p.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
					  p.Equals("obj", StringComparison.OrdinalIgnoreCase) ||
					  p.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
					  p.Equals(".idea", StringComparison.OrdinalIgnoreCase) ||
					  p.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
					  p.Equals(".dart_tool", StringComparison.OrdinalIgnoreCase) ||
					  p.Equals("build", StringComparison.OrdinalIgnoreCase));

	[GeneratedRegex(@"\(([^)]+)\)$")]
	private static partial Regex TfmRegex();
}
