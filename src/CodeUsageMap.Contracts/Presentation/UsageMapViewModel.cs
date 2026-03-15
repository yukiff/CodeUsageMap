using CodeUsageMap.Contracts.Diagnostics;

namespace CodeUsageMap.Contracts.Presentation;

public sealed class UsageMapViewModel
{
    public required string Title { get; init; }

    public required UsageMapNodeViewModel RootNode { get; init; }

    public UsageMapSymbolResolutionViewModel SymbolResolution { get; init; } = new();

    public UsageMapSummaryViewModel Summary { get; init; } = new();

    public IReadOnlyList<UsageMapNodeViewModel> Nodes { get; init; } = [];

    public IReadOnlyList<UsageMapEdgeViewModel> Edges { get; init; } = [];

    public IReadOnlyList<UsageMapRelationViewModel> IncomingRelations { get; init; } = [];

    public IReadOnlyList<UsageMapRelationViewModel> OutgoingRelations { get; init; } = [];

    public IReadOnlyList<UsageMapRelationViewModel> RelatedRelations { get; init; } = [];

    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; init; } = [];
}
