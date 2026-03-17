using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeToNeo4j.Solution;

public class MsBuildWorkspaceFactory : IWorkspaceFactory
{
    public IManagedWorkspace Create() => new MsBuildManagedWorkspace();

    private sealed class MsBuildManagedWorkspace : IManagedWorkspace
    {
        private readonly MSBuildWorkspace _workspace = MSBuildWorkspace.Create(new Dictionary<string, string>
        {
            // Design-time only: skip targets that validate installed SDKs (e.g. JDK for Android/MAUI)
            ["DesignTimeBuild"] = "true",
            ["BuildingInsideVisualStudio"] = "true",
            ["SkipCompilerExecution"] = "true",
            // Suppress restore and build targets irrelevant to code analysis
            ["ResolveAssemblyReferencesSilently"] = "true",
        });

        public Task<Microsoft.CodeAnalysis.Solution> OpenSolutionAsync(string solutionPath)
            => _workspace.OpenSolutionAsync(solutionPath);

        public async Task<Microsoft.CodeAnalysis.Solution> OpenProjectAsync(string projectPath)
        {
            var project = await _workspace.OpenProjectAsync(projectPath).ConfigureAwait(false);
            return project.Solution;
        }

        public void RegisterWorkspaceFailedHandler(Action<WorkspaceDiagnosticEventArgs> handler)
            => _workspace.RegisterWorkspaceFailedHandler(e => handler(e));

        public void Dispose() => _workspace.Dispose();
    }
}
