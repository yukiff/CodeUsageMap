using CodeUsageMap.Contracts.Diagnostics;

namespace CodeUsageMap.Contracts.Presentation;

public sealed class GraphCanvasViewModel
{
    public required string Title { get; init; }

    public GraphCanvasDisplayMode DisplayMode { get; init; } = GraphCanvasDisplayMode.CallMap;

    public required string RootNodeId { get; init; }

    public IReadOnlyList<CanvasNodeViewModel> Nodes { get; init; } = [];

    public IReadOnlyList<CanvasEdgeViewModel> Edges { get; init; } = [];

    public UsageMapSummaryViewModel Summary { get; init; } = new();

    public UsageMapSymbolResolutionViewModel SymbolResolution { get; init; } = new();

    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; init; } = [];
}
