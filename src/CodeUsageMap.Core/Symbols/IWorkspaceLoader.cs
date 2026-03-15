namespace CodeUsageMap.Core.Symbols;

public interface IWorkspaceLoader
{
    Task<LoadedSolution> LoadAsync(string solutionPath, CancellationToken cancellationToken);
}
