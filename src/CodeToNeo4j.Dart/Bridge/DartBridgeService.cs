using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using CodeToNeo4j.Dart.Models;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.Dart.Bridge;

public class DartBridgeService(ILogger<DartBridgeService> logger) : IDartBridgeService
{
    public async Task<DartAnalysisResult?> AnalyzeProject(string projectRoot)
    {
        if (_cache.TryGetValue(projectRoot, out var cached))
            return cached;

        var dartExecutable = FindDartExecutable();
        if (dartExecutable is null)
        {
            logger.LogWarning("Dart SDK not found on PATH. Skipping Dart analysis for {ProjectRoot}", projectRoot);
            _cache[projectRoot] = null;
            return null;
        }

        var bridgeDir = EnsureBridgeExtracted();
        if (bridgeDir is null)
        {
            logger.LogWarning("Failed to extract Dart analyzer bridge. Skipping Dart analysis for {ProjectRoot}", projectRoot);
            _cache[projectRoot] = null;
            return null;
        }

        if (!await EnsureDartPubGet(dartExecutable, bridgeDir).ConfigureAwait(false))
        {
            logger.LogWarning("dart pub get failed for the analyzer bridge. Skipping Dart analysis for {ProjectRoot}", projectRoot);
            _cache[projectRoot] = null;
            return null;
        }

        logger.LogInformation("Running Dart analyzer bridge for {ProjectRoot}...", projectRoot);

        try
        {
            var bridgeScript = Path.Combine(bridgeDir, "bin", "dart_analyzer_bridge.dart");
            var psi = new ProcessStartInfo
            {
                FileName = dartExecutable,
                ArgumentList = { "run", bridgeScript, projectRoot },
                WorkingDirectory = bridgeDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                logger.LogWarning("Failed to start Dart analyzer bridge process");
                _cache[projectRoot] = null;
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            var completed = process.WaitForExit((int)_defaultTimeout.TotalMilliseconds);
            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                logger.LogWarning("Dart analyzer bridge timed out after {Timeout}", _defaultTimeout);
                _cache[projectRoot] = null;
                return null;
            }

            var stderr = await stderrTask.ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(stderr))
            {
                logger.LogDebug("Dart analyzer bridge stderr: {Stderr}", stderr);
            }

            if (process.ExitCode != 0)
            {
                logger.LogWarning("Dart analyzer bridge exited with code {ExitCode}", process.ExitCode);
                _cache[projectRoot] = null;
                return null;
            }

            var stdout = await stdoutTask.ConfigureAwait(false);
            var result = JsonSerializer.Deserialize<DartAnalysisResult>(stdout);
            _cache[projectRoot] = result;
            logger.LogInformation("Dart analysis complete: {FileCount} files analyzed", result?.Files.Count ?? 0);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error running Dart analyzer bridge for {ProjectRoot}", projectRoot);
            _cache[projectRoot] = null;
            return null;
        }
    }

    internal static string? FindDartExecutable()
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var separator = OperatingSystem.IsWindows() ? ';' : ':';
        var exeName = OperatingSystem.IsWindows() ? "dart.exe" : "dart";

        foreach (var dir in pathVar.Split(separator))
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir, exeName);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Extracts embedded Dart bridge sources to a versioned cache directory.
    /// Returns the cache directory path, or null on failure.
    /// </summary>
    internal string? EnsureBridgeExtracted()
    {
        if (_bridgeDir is not null && Directory.Exists(_bridgeDir))
            return _bridgeDir;

        lock (_extractLock)
        {
            // Double-check after acquiring lock
            if (_bridgeDir is not null && Directory.Exists(_bridgeDir))
                return _bridgeDir;

            try
            {
                var assembly = typeof(DartBridgeService).Assembly;
                var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                              ?? assembly.GetName().Version?.ToString()
                              ?? "0.0.0";

                // Sanitize version for use as a directory name (e.g. strip +metadata)
                var safeVersion = string.Concat(version.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

                var cacheRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".codetoneo4j",
                    "dart-analyzer",
                    safeVersion);

                // Check if already extracted (sentinel file)
                var sentinel = Path.Combine(cacheRoot, ".extracted");
                if (File.Exists(sentinel))
                {
                    _bridgeDir = cacheRoot;
                    return cacheRoot;
                }

                logger.LogInformation("Extracting Dart analyzer bridge to {CacheDir}...", cacheRoot);

                // Clean and recreate
                if (Directory.Exists(cacheRoot))
                    Directory.Delete(cacheRoot, recursive: true);

                foreach (var (resourceName, relativePath) in _bridgeFiles)
                {
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream is null)
                    {
                        logger.LogWarning("Embedded resource not found: {ResourceName}", resourceName);
                        return null;
                    }

                    var targetPath = Path.Combine(cacheRoot, relativePath);
                    var targetDir = Path.GetDirectoryName(targetPath)!;
                    Directory.CreateDirectory(targetDir);

                    using var fileStream = File.Create(targetPath);
                    stream.CopyTo(fileStream);
                }

                // Write sentinel so we skip extraction next time
                File.WriteAllText(sentinel, version);

                _bridgeDir = cacheRoot;
                return cacheRoot;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to extract Dart analyzer bridge");
                return null;
            }
        }
    }

    /// <summary>
    /// Runs <c>dart pub get</c> in the bridge directory if dependencies haven't been resolved yet.
    /// </summary>
    internal async Task<bool> EnsureDartPubGet(string dartExecutable, string bridgeDir)
    {
        // .dart_tool/package_config.json is created by `dart pub get`
        var packageConfig = Path.Combine(bridgeDir, ".dart_tool", "package_config.json");
        if (File.Exists(packageConfig))
            return true;

        await _pubGetLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Double-check after acquiring lock
            if (File.Exists(packageConfig))
                return true;

            logger.LogInformation("Running dart pub get for analyzer bridge...");

            var psi = new ProcessStartInfo
            {
                FileName = dartExecutable,
                ArgumentList = { "pub", "get" },
                WorkingDirectory = bridgeDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = Process.Start(psi);
            if (process is null) return false;

            var stderrTask = process.StandardError.ReadToEndAsync();
            var completed = process.WaitForExit(60_000);
            if (!completed)
            {
                process.Kill(entireProcessTree: true);
                logger.LogWarning("dart pub get timed out");
                return false;
            }

            if (process.ExitCode != 0)
            {
                var stderr = await stderrTask.ConfigureAwait(false);
                logger.LogWarning("dart pub get failed (exit {ExitCode}): {Stderr}", process.ExitCode, stderr);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error running dart pub get");
            return false;
        }
        finally
        {
            _pubGetLock.Release();
        }
    }

    private readonly Dictionary<string, DartAnalysisResult?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private string? _bridgeDir;

    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromMinutes(5);
    private static readonly object _extractLock = new();
    private static readonly SemaphoreSlim _pubGetLock = new(1, 1);

    // Embedded resource logical names → relative file paths inside the extracted directory.
    private static readonly (string ResourceName, string RelativePath)[] _bridgeFiles =
    [
        ("dart-bridge.pubspec.yaml", "pubspec.yaml"),
        ("dart-bridge.bin.dart_analyzer_bridge.dart", Path.Combine("bin", "dart_analyzer_bridge.dart")),
        ("dart-bridge.lib.src.analyzer_service.dart", Path.Combine("lib", "src", "analyzer_service.dart")),
        ("dart-bridge.lib.src.ast_visitor.dart", Path.Combine("lib", "src", "ast_visitor.dart")),
        ("dart-bridge.lib.src.models.dart", Path.Combine("lib", "src", "models.dart")),
        ("dart-bridge.lib.src.json_output.dart", Path.Combine("lib", "src", "json_output.dart")),
    ];
}
