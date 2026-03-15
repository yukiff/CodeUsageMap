using CodeUsageMap.Contracts.Diagnostics;

namespace CodeUsageMap.Contracts.Presentation
{

public sealed class GraphCanvasViewModel
{
    public required string Title { get; init; }

    public GraphCanvasDisplayMode DisplayMode { get; init; } = GraphCanvasDisplayMode.CallMap;

    public required string RootNodeId { get; init; }

    public IReadOnlyList<CanvasNodeViewModel> Nodes { get; init; } = System.Array.Empty<CanvasNodeViewModel>();

    public IReadOnlyList<CanvasEdgeViewModel> Edges { get; init; } = System.Array.Empty<CanvasEdgeViewModel>();

    public UsageMapSummaryViewModel Summary { get; init; } = new();

    public UsageMapSymbolResolutionViewModel SymbolResolution { get; init; } = new();

    public IReadOnlyList<AnalysisDiagnostic> Diagnostics { get; init; } = System.Array.Empty<AnalysisDiagnostic>();
}
}
