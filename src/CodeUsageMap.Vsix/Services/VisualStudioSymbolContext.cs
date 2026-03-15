using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Vsix.Services
{

internal sealed class VisualStudioSymbolContext
{
    public required string SolutionPath { get; init; }

    public required string SymbolName { get; init; }

    public required string DisplayName { get; init; }

    public required string SymbolKey { get; init; }

    public NodeKind Kind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }
}
}
