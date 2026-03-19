using System.CommandLine;
using System.Reflection;
using CodeToNeo4j.ProgramOptions;
using CodeToNeo4j.ProgramOptions.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace CodeToNeo4j;

public class Program
{
	// Each entry is (Extension/Pattern, HandlerName) — kept in sync with ContainerModule handler registrations.
	internal static readonly (string Extension, string HandlerName)[] SupportedFileTypes =
	[
		(".cs", "CSharpHandler"),
		(".razor", "RazorHandler"),
		(".xaml", "XamlHandler"),
		(".js", "JavaScriptHandler"),
		(".ts / .tsx", "TypeScriptHandler"),
		(".html", "HtmlHandler"),
		(".xml", "XmlHandler"),
		("package.json", "PackageJsonHandler"),
		(".json", "JsonHandler"),
		(".css", "CssHandler"),
		(".csproj", "CsprojHandler"),
		(".dart", "DartHandler"),
		("pubspec.yaml", "PubspecYamlHandler")
	];

	public static async Task<int> Main(string[] args)
	{
		try
		{
			return await Run(args);
		}
		catch (Exception ex)
		{
			await Console.Error.WriteLineAsync(ex.ToString());
			return ex.HResult != 0 ? ex.HResult : 1;
		}
	}

	public static RootCommand CreateRootCommand()
	{
		string[] allSupportedExtensions = [".cs", ".razor", ".xaml", ".js", ".ts", ".tsx", ".html", ".xml", ".json", ".css", ".csproj", ".dart"];
		var uriOption = new Option<string>("--uri")
			.IsRequired()
			.WithDefaultValueFunc(() => "bolt://localhost:7687")
			.WithDescription("The Neo4j connection string.")
			.WithAlias("-u")
			.WithAlias("--url");
		var inputOption = new Option<string?>("--input")
			.WithAlias("--sln")
			.WithAlias("-s")
			.WithDescription("Path to a .sln, .slnx, or .csproj file, or a directory path. Auto-detects when omitted.");
		var passOption = new Option<string>("--password")
			.WithDescription("Password for the Neo4j database. Example: your-pass")
			.WithAlias("-p");
		var noKeyOption = new Option<bool>("--no-key")
			.WithDescription("Do not use a repository key. Use this if the Neo4j instance is dedicated to this repository.");
		var userOption = new Option<string>("--user")
			.WithDefaultValueFunc(() => "neo4j")
			.WithDescription("Neo4j username.");
		var databaseOption = new Option<string>("--database")
			.WithDefaultValueFunc(() => "neo4j")
			.WithDescription("Neo4j database name.")
			.WithAlias("-db");
		var diffBaseOption = new Option<string?>("--diff-base")
			.WithDescription("Optional git base ref for incremental indexing. Example: origin/main");
		var batchSizeOption = new Option<int>("--batch-size")
			.WithDefaultValueFunc(() => 500)
			.WithDescription("Number of symbols to batch before flushing to Neo4j.");
		var logLevelOption = new Option<LogLevel>("--log-level")
			.WithDefaultValueFunc(() => LogLevel.Information)
			.WithDescription("The minimum log level to display.")
			.WithAlias("-l");
		var skipDependenciesOption = new Option<bool>("--skip-dependencies")
			.WithDefaultValueFunc(() => false)
			.WithDescription("Skip NuGet dependency ingestion. Example: --skip-dependencies");
		var minAccessibilityOption = new Option<Accessibility>("--min-accessibility")
			.WithDefaultValueFunc(() => Accessibility.NotApplicable)
			.WithArgumentHelpName(string.Join('|', Accessibility.Private, Accessibility.Internal, Accessibility.Protected, Accessibility.Public))
			.WithDescription("The minimum accessibility level to index.");
		var includeExtensionsOption = new Option<string[]>("--include")
			.WithDefaultValueFunc(() => allSupportedExtensions)
			.WithDescription($"File extensions to include. Example: --include .cs --include .razor")
			.WithAlias("-i");
		var debugOption = new Option<bool>("--debug")
			.WithDescription("Turn on debug logging. Example: --debug")
			.WithAlias("-d");
		var verboseOption = new Option<bool>("--verbose")
			.WithDescription("Turn on trace logging. Example: --verbose")
			.WithAlias("-v");
		var quietOption = new Option<bool>("--quiet")
			.WithDescription("Mute all logging output. Example: --quiet")
			.WithAlias("-q");
		var purgeDataOption = new Option<bool>("--purge-data")
			.WithDefaultValueFunc(() => false)
			.WithDescription("Purge all data from Neo4j associated with the specified repository key. Example: --purge-data");
		var showVersionOption = new Option<bool>("--version")
			.WithDescription("Print the current tool version and exit.");
		var showSupportedFilesOption = new Option<bool>("--supported-files")
			.WithDescription("Print a table of all supported file types and exit.");
		var showInfoOption = new Option<bool>("--info")
			.WithDescription("Print the version and all supported file types, then exit.");

		OptionsBinder binder = new(
			new System.IO.Abstractions.FileSystem(),
			inputOption,
			uriOption,
			userOption,
			passOption,
			noKeyOption,
			diffBaseOption,
			batchSizeOption,
			databaseOption,
			minAccessibilityOption,
			logLevelOption,
			debugOption,
			verboseOption,
			quietOption,
			skipDependenciesOption,
			purgeDataOption,
			includeExtensionsOption,
			showVersionOption,
			showSupportedFilesOption,
			showInfoOption);

		RootCommand root = new("Index .NET solution into Neo4j via Roslyn");

		binder.AddToCommand(root);
		root.SetAction(async (parseResult, _) =>
		{
			Options options;
			try
			{
				options = binder.Bind(parseResult);
			}
			catch (InvalidOperationException ex)
			{
				await Console.Error.WriteLineAsync(ex.Message);
				return 1;
			}

			if (options.ShowVersion || options.ShowInfo)
			{
				Console.WriteLine($"CodeToNeo4j {GetVersion()}");
			}

			if (options.ShowSupportedFiles || options.ShowInfo)
			{
				PrintSupportedFiles();
			}

			if (options.ShowVersion || options.ShowSupportedFiles || options.ShowInfo)
			{
				return 0;
			}

			await using var services = new ServiceCollection()
				.AddApplicationServices(options.Uri, options.User, options.Pass!, options.LogLevel)
				.BuildServiceProvider();

			var logger = services.GetRequiredService<ILogger<Program>>();
			logger.LogInformation("CodeToNeo4j version {Version}", GetVersion());
			logger.LogInformation("{Options}", options);
			var handlers = services.GetRequiredService<IEnumerable<IOptionsHandler>>();
			await handlers.BuildChain().Handle(options);
			return 0;
		});

		return root;
	}

	internal static string GetVersion() =>
		typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? typeof(Program).Assembly.GetName().Version?.ToString()
		?? "unknown";

	internal static void PrintSupportedFiles()
	{
		const int extWidth = 20;
		var separator = new string('─', extWidth) + "  " + new string('─', 20);

		Console.WriteLine("Supported file types:");
		Console.WriteLine($"  {"Extension/Pattern",-20}  Handler");
		Console.WriteLine($"  {separator}");
		foreach (var (ext, handler) in SupportedFileTypes)
		{
			Console.WriteLine($"  {ext,-20}  {handler}");
		}
	}

	private static async Task<int> Run(string[] args)
	{
		var root = CreateRootCommand();
		return await root.Parse(args).InvokeAsync();
	}
}
