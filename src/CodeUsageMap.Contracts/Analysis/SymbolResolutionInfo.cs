namespace CodeUsageMap.Contracts.Analysis
{

public sealed class SymbolResolutionInfo
{
    public SymbolResolutionStatus Status { get; init; } = SymbolResolutionStatus.Unspecified;

    public string RequestedSymbolName { get; init; } = string.Empty;

    public int? RequestedSymbolIndex { get; init; }

    public int? SelectedSymbolIndex { get; init; }

    public IReadOnlyList<SymbolResolutionCandidate> Candidates { get; init; } = System.Array.Empty<SymbolResolutionCandidate>();
}
}
