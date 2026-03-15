using Microsoft.CodeAnalysis;

namespace CodeUsageMap.Core.Symbols
{

public sealed class LoadedSolution
    : IDisposable
{
    public required Workspace Workspace { get; init; }

    public required Solution Solution { get; init; }

    public void Dispose()
    {
        Workspace.Dispose();
    }
}
}
