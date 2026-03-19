using System.CommandLine;
using System.IO.Abstractions;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace CodeToNeo4j.ProgramOptions;

public class OptionsBinder(
	IFileSystem fileSystem,
	Option<string?> inputOption,
	Option<string> uriOption,
	Option<string> userOption,
	Option<string> passOption,
	Option<bool> noKeyOption,
	Option<string?> diffBaseOption,
	Option<int> batchSizeOption,
	Option<string> databaseOption,
	Option<Accessibility> minAccessibilityOption,
	Option<LogLevel> logLevelOption,
	Option<bool> debugOption,
	Option<bool> verboseOption,
	Option<bool> quietOption,
	Option<bool> skipDependenciesOption,
	Option<bool> purgeDataOption,
	Option<string[]> includeExtensionsOption,
	Option<bool> showVersionOption,
	Option<bool> showSupportedFilesOption,
	Option<bool> showInfoOption)
{
	public void AddToCommand(Command command)
	{
		// Remove the built-in VersionOption to avoid conflict with our custom --version flag
		var builtInVersion = command.Options.OfType<VersionOption>().FirstOrDefault();
		if (builtInVersion is not null)
		{
			command.Options.Remove(builtInVersion);
		}

		command.Options.Add(inputOption);
		command.Options.Add(uriOption);
		command.Options.Add(userOption);
		command.Options.Add(passOption);
		command.Options.Add(noKeyOption);
		command.Options.Add(diffBaseOption);
		command.Options.Add(batchSizeOption);
		command.Options.Add(databaseOption);
		command.Options.Add(minAccessibilityOption);
		command.Options.Add(logLevelOption);
		command.Options.Add(debugOption);
		command.Options.Add(verboseOption);
		command.Options.Add(quietOption);
		command.Options.Add(skipDependenciesOption);
		command.Options.Add(purgeDataOption);
		command.Options.Add(includeExtensionsOption);
		command.Options.Add(showVersionOption);
		command.Options.Add(showSupportedFilesOption);
		command.Options.Add(showInfoOption);

		command.Validators.Add(result => OptionsBinderValidator.Validate(
			result,
			inputOption,
			noKeyOption,
			logLevelOption,
			debugOption,
			verboseOption,
			quietOption,
			purgeDataOption,
			skipDependenciesOption,
			minAccessibilityOption,
			passOption,
			showVersionOption,
			showSupportedFilesOption,
			showInfoOption));
	}

	public Options Bind(ParseResult parseResult)
	{
		var isInfo = parseResult.GetValue(showVersionOption)
					 || parseResult.GetValue(showSupportedFilesOption)
					 || parseResult.GetValue(showInfoOption);

		var rawInput = parseResult.GetValue(inputOption);
		InputPathResolver resolver = new(fileSystem);
		var inputPath = isInfo
			? rawInput ?? fileSystem.Directory.GetCurrentDirectory()
			: resolver.Resolve(rawInput);

		var noKey = parseResult.GetValue(noKeyOption);
		var derivedName = fileSystem.Path.GetFileNameWithoutExtension(
			inputPath.TrimEnd(fileSystem.Path.DirectorySeparatorChar, fileSystem.Path.AltDirectorySeparatorChar));
		if (string.IsNullOrEmpty(derivedName))
		{
			derivedName = fileSystem.Path.GetFileName(
				fileSystem.Directory.GetCurrentDirectory()
					.TrimEnd(fileSystem.Path.DirectorySeparatorChar, fileSystem.Path.AltDirectorySeparatorChar));
		}

		var repoKey = noKey ? null : derivedName.ToLowerInvariant();

		IFileSystemInfo inputFsi = fileSystem.Directory.Exists(inputPath)
			? fileSystem.DirectoryInfo.New(inputPath)
			: fileSystem.FileInfo.New(inputPath);

		return new(
			inputFsi,
			repoKey,
			parseResult.GetValue(uriOption)!,
			parseResult.GetValue(userOption)!,
			parseResult.GetValue(passOption),
			noKey,
			parseResult.GetValue(diffBaseOption),
			parseResult.GetValue(batchSizeOption),
			parseResult.GetValue(databaseOption)!,
			ParseLogLevel(parseResult),
			parseResult.GetValue(skipDependenciesOption),
			parseResult.GetValue(minAccessibilityOption),
			parseResult.GetValue(includeExtensionsOption)!,
			parseResult.GetValue(purgeDataOption),
			parseResult.GetValue(showVersionOption),
			parseResult.GetValue(showSupportedFilesOption),
			parseResult.GetValue(showInfoOption)
		);
	}

	private LogLevel ParseLogLevel(ParseResult parseResult)
	{
		var logLevel = parseResult.GetValue(logLevelOption);
		if (parseResult.GetValue(debugOption))
		{
			logLevel = LogLevel.Debug;
		}
		else if (parseResult.GetValue(verboseOption))
		{
			logLevel = LogLevel.Trace;
		}
		else if (parseResult.GetValue(quietOption))
		{
			logLevel = LogLevel.None;
		}

		return logLevel;
	}
}
