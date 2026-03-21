using System.IO.Abstractions;
using System.Text.RegularExpressions;
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
		Dictionary<string, ProcessedFile> solutionFiles = new(StringComparer.OrdinalIgnoreCase);

		// 1. Get all documents from MSBuild (when a solution/project is loaded)
		if (solution is not null)
		{
			foreach (var project in solution.Projects)
			{
				// Skip outer multi-target wrapper projects (they have 0 documents and no useful data)
				if (!project.Documents.Any() && !project.AdditionalDocuments.Any())
				{
					continue;
				}

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

					if (!solutionFiles.ContainsKey(path))
					{
						solutionFiles[path] = new(path, project.Id, doc.Id);
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

					if (!solutionFiles.ContainsKey(path))
					{
						solutionFiles[path] = new(path, project.Id, doc.Id);
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
				solutionFiles[normalizedPath] = new(normalizedPath, null, null);
			}
		}

		return solutionFiles.Values;
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
