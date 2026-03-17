using Microsoft.CodeAnalysis;

namespace CodeToNeo4j.Solution;

public interface IManagedWorkspace : IDisposable
{
    Task<Microsoft.CodeAnalysis.Solution> OpenSolutionAsync(string solutionPath);
    Task<Microsoft.CodeAnalysis.Solution> OpenProjectAsync(string projectPath);
    void RegisterWorkspaceFailedHandler(Action<WorkspaceDiagnosticEventArgs> handler);
}
