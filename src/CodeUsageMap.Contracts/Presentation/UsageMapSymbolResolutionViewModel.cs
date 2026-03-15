using CodeUsageMap.Contracts.Analysis;

namespace CodeUsageMap.Contracts.Presentation;

public sealed class UsageMapSymbolResolutionViewModel
{
    public SymbolResolutionStatus Status { get; init; } = SymbolResolutionStatus.Unspecified;

    public string RequestedSymbolName { get; init; } = string.Empty;

    public int? RequestedSymbolIndex { get; init; }

    public int? SelectedSymbolIndex { get; init; }

    public IReadOnlyList<UsageMapSymbolCandidateViewModel> Candidates { get; init; } = [];
}
