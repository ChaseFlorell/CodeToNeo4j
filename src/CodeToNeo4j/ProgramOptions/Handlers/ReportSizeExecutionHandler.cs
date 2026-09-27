using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Models;

namespace CodeToNeo4j.ProgramOptions.Handlers;

public class ReportSizeExecutionHandler(IGraphService graphService) : OptionsHandler
{
	internal static void PrintReport(IReadOnlyList<ProjectSizeReport> report)
	{
		Console.WriteLine("Codebase size report (by node count):");

		if (report.Count == 0)
		{
			Console.WriteLine("  No codebases found.");
			return;
		}

		(string Header, int Width, bool LeftAlign)[] columns =
		[
			("Repo Key", 22, true),
			("Files", 7, false),
			("Symbols", 9, false),
			("Dependencies", 14, false),
			("Commits", 9, false),
			("Authors", 9, false),
			("Total", 9, false)
		];

		string Pad(string value, (string Header, int Width, bool LeftAlign) column) =>
			column.LeftAlign ? value.PadRight(column.Width) : value.PadLeft(column.Width);

		var headerLine = "  " + string.Join(string.Empty, columns.Select(c => Pad(c.Header, c)));
		var separatorLine = "  " + string.Join(string.Empty, columns.Select(c => new string('─', c.Width)));

		Console.WriteLine(headerLine);
		Console.WriteLine(separatorLine);
		foreach (var project in report)
		{
			string[] values =
			[
				project.RepoKey,
				project.FileCount.ToString(),
				project.SymbolCount.ToString(),
				project.DependencyCount.ToString(),
				project.CommitCount.ToString(),
				project.AuthorCount.ToString(),
				project.TotalCount.ToString()
			];
			Console.WriteLine("  " + string.Join(string.Empty, values.Select((v, i) => Pad(v, columns[i]))));
		}
	}

	protected override async Task<bool> HandleOptions(Options options)
	{
		if (options.ReportSize)
		{
			var report = await graphService.GetProjectSizeReport(options.DatabaseName);
			PrintReport(report);
			return false;
		}

		return true;
	}
}
