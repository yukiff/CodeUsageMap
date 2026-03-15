using CodeUsageMap.Contracts.Diagnostics;

namespace CodeUsageMap.Contracts.Presentation
{

public sealed class UsageMapViewModel
{
    public required string Title { get; init; }

    public required UsageMapNodeViewModel RootNode { get; init; }

    public UsageMapSymbolResolutionViewModel SymbolResolution { get; init; } = new();

    public UsageMapSummaryViewModel Summary { get; init; } = new();

    public IReadOnlyList<UsageMapNodeViewModel> Nodes { get; init; } = System.Array.Empty<UsageMapNodeViewModel>();

    public IReadOnlyList<UsageMapEdgeViewModel> Edges { get; init; } = System.Array.Empty<UsageMapEdgeViewModel>();

    public IReadOnlyList<UsageMapRelationViewModel> IncomingRelations { get; init; } = System.Array.Empty<UsageMapRelationViewModel>();

    public IReadOnlyList<UsageMapRelationViewModel> OutgoingRelations { get; init; } = System.Array.Empty<UsageMapRelationViewModel>();

    public IReadOnlyList<UsageMapRelationViewModel> RelatedRelations { get; init; } = System.Array.Empty<UsageMapRelationViewModel>();

    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; init; } = System.Array.Empty<AnalysisDiagnostic>();
}
}
