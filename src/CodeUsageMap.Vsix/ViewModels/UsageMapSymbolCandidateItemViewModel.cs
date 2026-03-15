using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Vsix.ViewModels
{

internal sealed class UsageMapSymbolCandidateItemViewModel
{
    public int Index { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string ProjectName { get; init; } = string.Empty;

    public string MatchKind { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public NodeKind Kind { get; init; } = NodeKind.Unknown;
}
}
