using System.Diagnostics;
using System.IO.Abstractions;
using System.Threading.Channels;
using CodeToNeo4j.FileHandlers;
using CodeToNeo4j.FileSystem;
using CodeToNeo4j.Graph;
using CodeToNeo4j.Progress;
using CodeToNeo4j.VersionControl;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Solution;

public class SolutionProcessor(
	IVersionControlService versionControlService,
	IGraphService graphService,
	IFileService fileService,
	IFileSystem fileSystem,
	IProgressService progressService,
	IEnumerable<IDocumentHandler> handlers,
	IDependencyIngestor dependencyIngestor,
	ISolutionFileDiscoveryService discoveryService,
	ICommitIngestionService commitIngestionService,
	IWorkspaceFactory workspaceFactory,
	ILogger<SolutionProcessor> logger) : ISolutionProcessor
{
	private readonly HandlerLookup _handlerLookup = new(handlers, fileSystem);

	public async Task ProcessSolution(string inputPath, string? repoKey, string? diffBase, string databaseName, int batchSize, bool skipDependencies,
		Accessibility minAccessibility, IEnumerable<string> includeExtensions)
	{
		Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
		HashSet<string> extensionsToInclude = includeExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);

		var ext = fileSystem.Path.GetExtension(inputPath);
		var isProjectFile = ext is ".sln" or ".slnx" or ".csproj";
		var solutionRoot = isProjectFile
			? fileService.NormalizePath(fileSystem.Path.GetDirectoryName(inputPath) ?? fileSystem.Directory.GetCurrentDirectory())
			: fileService.NormalizePath(inputPath);

		Microsoft.CodeAnalysis.Solution? solution = null;
		IManagedWorkspace? workspace = null;

		try
		{
			if (isProjectFile)
			{
				logger.LogInformation("Processing: {InputPath}", inputPath);
				workspace = workspaceFactory.Create();
				workspace.RegisterWorkspaceFailedHandler(e => logger.LogWarning("Workspace warning: {Message}", e.Diagnostic.Message));

				if (ext is ".csproj")
				{
					logger.LogInformation("Opening project...");
					solution = await workspace.OpenProjectAsync(inputPath).ConfigureAwait(false);
					logger.LogInformation("Project opened successfully.");
				}
				else
				{
					logger.LogInformation("Opening solution...");
					solution = await workspace.OpenSolutionAsync(inputPath).ConfigureAwait(false);
					logger.LogInformation("Solution opened successfully.");
				}
			}
			else
			{
				logger.LogWarning("No project file detected. Running in files-only mode for directory: {Directory}", solutionRoot);
			}

			// Start git metadata loading in background so it overlaps with dependency ingestion and diff computation
			var metadataTask = versionControlService.LoadMetadata(solutionRoot, extensionsToInclude);

			if (!skipDependencies && solution is not null)
			{
				await dependencyIngestor.IngestDependencies(solution, repoKey, databaseName).ConfigureAwait(false);
			}

			var diffResult = await GetChangedFiles(solutionRoot, diffBase, extensionsToInclude).ConfigureAwait(false);

			if (diffResult?.DeletedFiles.Count > 0)
			{
				logger.LogInformation("Marking {Count} files as deleted that were removed in git...", diffResult.DeletedFiles.Count);
				foreach (var deletedFile in diffResult.DeletedFiles)
				{
					var relativePath = fileService.GetRelativePath(solutionRoot, deletedFile);
					await graphService.MarkFileAsDeleted(relativePath, databaseName).ConfigureAwait(false);
				}
			}

			ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) };

			var discoveredFiles = discoveryService.GetFilesToProcess(solutionRoot, solution, extensionsToInclude);
			var filesToProcess = FilterFiles(discoveredFiles, diffResult?.ModifiedFiles);
			var totalFiles = filesToProcess.Length;

			if (totalFiles == 0)
			{
				logger.LogInformation("No files found to process. If this is an incremental run, check your diff-base.");
				return;
			}

			logger.LogInformation("Processing {Count} files in: {InputPath}...", totalFiles,
				fileSystem.Path.GetFileName(inputPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar, fileSystem.Path.AltDirectorySeparatorChar)));

			// Ensure git metadata is fully loaded before processing files
			await metadataTask.ConfigureAwait(false);
			Channel<ProcessResult> channel = Channel.CreateBounded<ProcessResult>(new BoundedChannelOptions(100)
			{
				FullMode = BoundedChannelFullMode.Wait,
				SingleReader = true
			});

			var consumerTask = RunConsumer(channel.Reader, totalFiles, databaseName, batchSize);

			await Parallel.ForEachAsync(filesToProcess, parallelOptions, async (file, t) =>
			{
				var result = await ProcessFile(solution, file, solutionRoot, repoKey, minAccessibility).ConfigureAwait(false);
				await channel.Writer.WriteAsync(result, t).ConfigureAwait(false);
			}).ConfigureAwait(false);

			channel.Writer.Complete();
			var (totalSymbols, totalRelationships) = await consumerTask.ConfigureAwait(false);
			progressService.ProgressComplete();

			if (diffBase is not null)
			{
				await commitIngestionService.IngestCommits(diffBase, solutionRoot, repoKey, databaseName, batchSize).ConfigureAwait(false);
			}

			logger.LogInformation("Processing complete.");
			logger.LogInformation("Total nodes (symbols) created: {Count}", totalSymbols);
			logger.LogInformation("Total relationships created: {Count}", totalRelationships);

			foreach (var handler in handlers.Where(h => h.NumberOfFilesHandled > 0))
			{
				logger.LogInformation("{FileExtension} files handled: {Count}", handler.FileExtension, handler.NumberOfFilesHandled);
			}

			var elapsed = stopwatch.Elapsed;
			var duration = elapsed.TotalHours >= 1
				? $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m {elapsed.Seconds}s"
				: elapsed.TotalMinutes >= 1
					? $"{elapsed.Minutes}m {elapsed.Seconds}s"
					: $"{elapsed.Seconds}s";
			logger.LogInformation("Done: {Duration}", duration);
		}
		finally
		{
			workspace?.Dispose();
		}
	}

	internal async Task<(int TotalSymbols, int TotalRelationships)> RunConsumer(ChannelReader<ProcessResult> reader, int totalFiles,
		string databaseName, int batchSize)
	{
		List<FileMetaData> fileBuffer = new(batchSize);
		List<Symbol> symbolBuffer = new(batchSize);
		List<Relationship> relBuffer = new(batchSize);
		List<UrlNode> urlBuffer = new(batchSize);
		List<TargetFrameworkBatch> tfmBuffer = new(batchSize);
		var currentFileIndex = 0;
		var totalSymbols = 0;
		var totalRelationships = 0;

		await foreach (var result in reader.ReadAllAsync().ConfigureAwait(false))
		{
			fileBuffer.Add(result.File);
			symbolBuffer.AddRange(result.Symbols);
			relBuffer.AddRange(result.Relationships);
			urlBuffer.AddRange(result.UrlNodes);

			if (result.TargetFrameworks is { Count: > 0 })
			{
				tfmBuffer.Add(new(
					result.File.FileKey,
					result.Symbols.Select(s => s.Key).ToArray(),
					result.TargetFrameworks.ToArray()));
			}

			totalSymbols += result.Symbols.Count;
			totalRelationships += result.Relationships.Count;

			if (fileBuffer.Count >= batchSize || symbolBuffer.Count >= batchSize)
			{
				await FlushBuffers(fileBuffer, symbolBuffer, relBuffer, urlBuffer, tfmBuffer, databaseName).ConfigureAwait(false);
			}

			progressService.ReportProgress(++currentFileIndex, totalFiles, result.RelativePath);
		}

		if (fileBuffer.Count > 0 || symbolBuffer.Count > 0 || urlBuffer.Count > 0 || tfmBuffer.Count > 0)
		{
			await FlushBuffers(fileBuffer, symbolBuffer, relBuffer, urlBuffer, tfmBuffer, databaseName).ConfigureAwait(false);
		}

		return (totalSymbols, totalRelationships);
	}

	private async Task FlushBuffers(List<FileMetaData> files, List<Symbol> symbols, List<Relationship> relationships, List<UrlNode> urlNodes,
		List<TargetFrameworkBatch> tfmBatches, string databaseName)
	{
		if (files.Count > 0)
		{
			await graphService.FlushFiles(files, databaseName).ConfigureAwait(false);
			files.Clear();
		}

		if (symbols.Count > 0 || relationships.Count > 0)
		{
			await graphService.FlushSymbols(symbols, relationships, databaseName).ConfigureAwait(false);
			symbols.Clear();
			relationships.Clear();
		}

		if (urlNodes.Count > 0)
		{
			await graphService.UpsertDependencyUrls(urlNodes, databaseName).ConfigureAwait(false);
			urlNodes.Clear();
		}

		if (tfmBatches.Count > 0)
		{
			await graphService.FlushTargetFrameworks(tfmBatches, databaseName).ConfigureAwait(false);
			tfmBatches.Clear();
		}
	}

	private async Task<DiffResult?> GetChangedFiles(string solutionRoot, string? diffBase, HashSet<string> includeExtensions)
	{
		if (diffBase is null)
		{
			return null;
		}

		var result = await versionControlService.GetChangedFiles(diffBase, solutionRoot, includeExtensions).ConfigureAwait(false);

		logger.LogInformation("Incremental indexing enabled. Found {ModifiedCount} modified and {DeletedCount} deleted files since {DiffBase}",
			result.ModifiedFiles.Count, result.DeletedFiles.Count, diffBase);

		return result;
	}

	internal async Task<ProcessResult> ProcessFile(
		Microsoft.CodeAnalysis.Solution? solution,
		ProcessedFile file,
		string solutionRoot,
		string? repoKey,
		Accessibility minAccessibility)
	{
		var filePath = file.FilePath;
		logger.LogDebug("Processing file: {FilePath}", filePath);

		var relativePath = fileService.GetRelativePath(solutionRoot, filePath);
		var (inferredKey, inferredNamespace) = fileService.InferFileMetadata(relativePath);
		var fileKey = inferredKey;
		var fileName = fileSystem.Path.GetFileName(filePath);
		var defaultNamespace = inferredNamespace;
		var fileHash = await fileService.ComputeSha256(filePath).ConfigureAwait(false);
		var metadata = await versionControlService.GetFileMetadata(filePath, solutionRoot).ConfigureAwait(false);

		List<Symbol> symbols = [];
		List<Relationship> relationships = [];

		TextDocument? document = null;
		Compilation? compilation = null;

		if (file.ProjectId != null && solution is not null)
		{
			var project = solution.GetProject(file.ProjectId);
			if (project != null)
			{
				if (file.DocumentId != null)
				{
					document = (TextDocument?)project.GetDocument(file.DocumentId) ?? project.GetAdditionalDocument(file.DocumentId);
				}

				try
				{
					compilation = await project.GetCompilationAsync().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					logger.LogDebug(ex,
						"Failed to get compilation for project {ProjectName}, skipping compilation for file {FilePath}. "
						+ "This is common with multi-target projects where the outer wrapper project lacks a Compile target. "
						+ "Semantic analysis will still occur for the specific target framework projects (e.g. net9.0, net8.0)",
						project.Name, filePath);
				}
			}
		}

		var handler = _handlerLookup.GetHandler(filePath);
		FileResult? fileResult = null;
		if (handler != null)
		{
			fileResult = await handler
				.Handle(document, compilation, repoKey, fileKey, filePath, relativePath, symbols, relationships, minAccessibility)
				.ConfigureAwait(false);
		}

		var finalFileKey = fileResult?.FileKey ?? fileKey;
		var finalNamespace = fileResult?.Namespace ?? defaultNamespace;
		var urlNodes = fileResult?.UrlNodes is { Count: > 0 } urls ? urls.ToList() : [];

		var language = handler?.Language ?? "unknown";
		// MSBuild-discovered TFMs take priority; fall back to handler-detected TFMs
		var effectiveTfms = file.TargetFrameworks is { Count: > 0 } ? file.TargetFrameworks : fileResult?.TargetFrameworks;
		FileMetaData fileRecord = new(finalFileKey, fileName, relativePath, fileHash, metadata, repoKey, finalNamespace, language, effectiveTfms);

		return new(fileRecord, symbols, relationships, urlNodes, relativePath, effectiveTfms);
	}

	internal static ProcessedFile[] FilterFiles(IEnumerable<ProcessedFile> discoveredFiles, HashSet<string>? changedFiles)
	{
		var result = discoveredFiles.ToArray();

		switch (changedFiles?.Any() ?? false)
		{
			case true:
				result = result
					.Where(f => changedFiles.Contains(f.FilePath))
					.ToArray();
				break;
			default:
				{
					if (changedFiles is not null)
					{
						result = [];
					}

					break;
				}
		}

		return result;
	}

	internal record ProcessResult(
		FileMetaData File,
		List<Symbol> Symbols,
		List<Relationship> Relationships,
		List<UrlNode> UrlNodes,
		string RelativePath,
		IReadOnlySet<string>? TargetFrameworks = null);

	internal sealed class HandlerLookup
	{
		private readonly IFileSystem _fileSystem;
		private readonly Dictionary<string, IDocumentHandler> _byFileName = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, IDocumentHandler> _byExtension = new(StringComparer.OrdinalIgnoreCase);

		public HandlerLookup(IEnumerable<IDocumentHandler> handlers, IFileSystem fileSystem)
		{
			_fileSystem = fileSystem;

			foreach (var handler in handlers)
			{
				foreach (var ext in handler.FileExtensions)
				{
					// Handlers whose extension is a full filename (e.g. "package.json")
					// are indexed by filename for O(1) lookup.
					if (!ext.StartsWith('.'))
					{
						_byFileName.TryAdd(ext, handler);
					}
					else
					{
						_byExtension.TryAdd(ext, handler);
					}
				}
			}
		}

		public IDocumentHandler? GetHandler(string filePath)
		{
			// O(1) filename lookup (e.g. package.json)
			var fileName = _fileSystem.Path.GetFileName(filePath);
			if (_byFileName.TryGetValue(fileName, out var byName))
			{
				return byName;
			}

			// O(1) extension lookup — all extensions from every handler are indexed
			var ext = _fileSystem.Path.GetExtension(filePath);
			if (!string.IsNullOrEmpty(ext) && _byExtension.TryGetValue(ext, out var byExt))
			{
				return byExt;
			}

			return null;
		}
	}
}
