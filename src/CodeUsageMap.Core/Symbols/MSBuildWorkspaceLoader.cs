using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis.MSBuild;

namespace CodeUsageMap.Core.Symbols;

public sealed class MSBuildWorkspaceLoader : IWorkspaceLoader
{
    private static readonly object Sync = new();
    private static bool _isRegistered;

    public async Task<LoadedSolution> LoadAsync(string solutionPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMsBuildRegistered();

        var workspace = MSBuildWorkspace.Create();
        workspace.LoadMetadataForReferencedProjects = true;
        var solution = await workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken);

        return new LoadedSolution
        {
            Workspace = workspace,
            Solution = solution,
        };
    }

    private static void EnsureMsBuildRegistered()
    {
        lock (Sync)
        {
            if (_isRegistered || MSBuildLocator.IsRegistered)
            {
                _isRegistered = true;
                return;
            }

            MSBuildLocator.RegisterDefaults();
            _isRegistered = true;
        }
    }
}
