namespace CodeUsageMap.Core.Symbols
{

public sealed class RoslynWorkspaceLoader : IWorkspaceLoader
{
    public RoslynWorkspaceLoader()
    {
    }

    public RoslynWorkspaceLoader(IWorkspaceLoader innerLoader)
    {
        _innerLoader = innerLoader;
    }

    private readonly IWorkspaceLoader? _innerLoader;

    public Task<LoadedSolution> LoadAsync(string solutionPath, CancellationToken cancellationToken)
    {
        return (_innerLoader ?? WorkspaceLoaderFactory.CreateDefault()).LoadAsync(solutionPath, cancellationToken);
    }

    public Task<LoadedSolution> LoadAsync(string solutionPath, string? preferredLoader, CancellationToken cancellationToken)
    {
        return (_innerLoader ?? WorkspaceLoaderFactory.Create(preferredLoader)).LoadAsync(solutionPath, cancellationToken);
    }
}
}
