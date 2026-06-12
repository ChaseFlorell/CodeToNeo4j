using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Abstractions;
using System.Reflection;
using System.Text.Json;
using CodeToNeo4j.TypeScript.Models;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.TypeScript.Bridge;

public class TypeScriptBridgeService(IFileSystem fileSystem, ILogger<TypeScriptBridgeService> logger) : ITypeScriptBridgeService
{
	[ExcludeFromCodeCoverage(Justification = "Requires live Node.js runtime and OS process execution")]
	public async Task<TsAnalysisResult?> AnalyzeProject(string projectRoot)
	{
		if (_cache.TryGetValue(projectRoot, out var cached))
		{
			return cached;
		}

		var nodeExecutable = FindNodeExecutable();
		if (nodeExecutable is null)
		{
			logger.LogWarning("Node.js not found on PATH. Skipping TypeScript/JavaScript analysis for {ProjectRoot}", projectRoot);
			_cache[projectRoot] = null;
			return null;
		}

		var bridgeDir = EnsureBridgeExtracted();
		if (bridgeDir is null)
		{
			logger.LogWarning("Failed to extract TypeScript analyzer bridge. Skipping analysis for {ProjectRoot}", projectRoot);
			_cache[projectRoot] = null;
			return null;
		}

		logger.LogInformation("Running TypeScript analyzer bridge for {ProjectRoot}...", projectRoot);

		try
		{
			var bridgeScript = fileSystem.Path.Combine(bridgeDir, "dist", "index.js");
			ProcessStartInfo psi = new()
			{
				FileName = nodeExecutable,
				ArgumentList = { bridgeScript, projectRoot },
				WorkingDirectory = bridgeDir,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true
			};

			using Process? process = Process.Start(psi);
			if (process is null)
			{
				logger.LogWarning("Failed to start TypeScript analyzer bridge process");
				_cache[projectRoot] = null;
				return null;
			}

			var stdoutTask = process.StandardOutput.ReadToEndAsync();
			var stderrTask = process.StandardError.ReadToEndAsync();

			var completed = process.WaitForExit((int)_defaultTimeout.TotalMilliseconds);
			if (!completed)
			{
				process.Kill(entireProcessTree: true);
				logger.LogWarning("TypeScript analyzer bridge timed out after {Timeout}", _defaultTimeout);
				_cache[projectRoot] = null;
				return null;
			}

			var stderr = await stderrTask.ConfigureAwait(false);
			if (!string.IsNullOrWhiteSpace(stderr))
			{
				logger.LogDebug("TypeScript analyzer bridge stderr: {Stderr}", stderr);
			}

			if (process.ExitCode != 0)
			{
				logger.LogWarning("TypeScript analyzer bridge exited with code {ExitCode}", process.ExitCode);
				_cache[projectRoot] = null;
				return null;
			}

			var stdout = await stdoutTask.ConfigureAwait(false);
			var result = JsonSerializer.Deserialize<TsAnalysisResult>(stdout);
			_cache[projectRoot] = result;
			logger.LogInformation("TypeScript analysis complete: {FileCount} files analyzed", result?.Files.Count ?? 0);
			return result;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Error running TypeScript analyzer bridge for {ProjectRoot}", projectRoot);
			_cache[projectRoot] = null;
			return null;
		}
	}

	/// <summary>
	/// Searches PATH for a <c>node</c> (or <c>node.exe</c> on Windows) executable.
	/// Returns the full path, or <see langword="null"/> if Node.js is not installed.
	/// </summary>
	internal string? FindNodeExecutable()
	{
		var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
		var separator = OperatingSystem.IsWindows() ? ';' : ':';
		var exeName = OperatingSystem.IsWindows() ? "node.exe" : "node";

		foreach (var dir in pathVar.Split(separator))
		{
			if (string.IsNullOrWhiteSpace(dir))
			{
				continue;
			}

			var candidate = fileSystem.Path.Combine(dir, exeName);
			if (fileSystem.File.Exists(candidate))
			{
				return candidate;
			}
		}

		return null;
	}

	/// <summary>
	/// Extracts the embedded ts-analyzer bundle to a versioned cache directory.
	/// Returns the cache directory path, or <see langword="null"/> on failure.
	/// </summary>
	internal string? EnsureBridgeExtracted()
	{
		if (_bridgeDir is not null && fileSystem.Directory.Exists(_bridgeDir))
		{
			return _bridgeDir;
		}

		lock (_extractLock)
		{
			// Double-check after acquiring lock
			if (_bridgeDir is not null && fileSystem.Directory.Exists(_bridgeDir))
			{
				return _bridgeDir;
			}

			try
			{
				var assembly = typeof(TypeScriptBridgeService).Assembly;
				var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
							  ?? assembly.GetName().Version?.ToString()
							  ?? "0.0.0";

				var safeVersion = string.Concat(version.Select(c => fileSystem.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

				var cacheRoot = fileSystem.Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
					".codetoneo4j",
					"ts-analyzer",
					safeVersion);

				var sentinel = fileSystem.Path.Combine(cacheRoot, ".extracted");
				if (fileSystem.File.Exists(sentinel))
				{
					_bridgeDir = cacheRoot;
					return cacheRoot;
				}

				logger.LogInformation("Extracting TypeScript analyzer bridge to {CacheDir}...", cacheRoot);

				if (fileSystem.Directory.Exists(cacheRoot))
				{
					fileSystem.Directory.Delete(cacheRoot, recursive: true);
				}

				foreach (var (resourceName, relativePath) in BridgeFiles)
				{
					using var stream = assembly.GetManifestResourceStream(resourceName);
					if (stream is null)
					{
						logger.LogWarning("Embedded resource not found: {ResourceName}", resourceName);
						return null;
					}

					var targetPath = fileSystem.Path.Combine(cacheRoot, relativePath);
					var targetDir = fileSystem.Path.GetDirectoryName(targetPath)!;
					fileSystem.Directory.CreateDirectory(targetDir);

					using var fileStream = fileSystem.File.Create(targetPath);
					stream.CopyTo(fileStream);
				}

				fileSystem.File.WriteAllText(sentinel, version);

				_bridgeDir = cacheRoot;
				return cacheRoot;
			}
			catch (Exception ex)
			{
				logger.LogWarning(ex, "Failed to extract TypeScript analyzer bridge");
				return null;
			}
		}
	}

	private readonly Dictionary<string, TsAnalysisResult?> _cache = new(StringComparer.OrdinalIgnoreCase);
	private string? _bridgeDir;

	private static readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);
	private static readonly object _extractLock = new();

	// Embedded resource logical names → relative file paths inside the extracted directory.
	private (string ResourceName, string RelativePath)[] BridgeFiles => field ??=
	[
		("ts-bridge.dist.index.js", fileSystem.Path.Combine("dist", "index.js"))
	];
}
