using CodeToNeo4j.Graph;
using CodeToNeo4j.Graph.Models;

namespace CodeToNeo4j.ProgramOptions.Handlers;

public class ReportSizeExecutionHandler(IGraphService graphService) : OptionsHandler
{
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

	internal static void PrintReport(IReadOnlyList<ProjectSizeReport> report)
	{
		Console.WriteLine("Codebase size report (by node count):");

		if (report.Count == 0)
		{
			Console.WriteLine("  No codebases found.");
			return;
		}

		const int keyWidth = 20;
		var separator = new string('─', keyWidth) + "  " + string.Join("  ", "─────", "───────", "────────────", "───────", "───────", "───────");

		Console.WriteLine($"  {"Repo Key",-keyWidth}  {"Files",5}   {"Symbols",7}  {"Dependencies",12}  {"Commits",7}  {"Authors",7}  {"Total",7}");
		Console.WriteLine($"  {separator}");
		foreach (var project in report)
		{
			Console.WriteLine(
				$"  {project.RepoKey,-keyWidth}  {project.FileCount,5}   {project.SymbolCount,7}  {project.DependencyCount,12}  {project.CommitCount,7}  {project.AuthorCount,7}  {project.TotalCount,7}");
		}
	}
}
