using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis
{

public sealed class SymbolResolutionCandidate
{
    public int Index { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string SymbolKey { get; init; } = string.Empty;

    public NodeKind Kind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public string MatchKind { get; init; } = string.Empty;
}
}
