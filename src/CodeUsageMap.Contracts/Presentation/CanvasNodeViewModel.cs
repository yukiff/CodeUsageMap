using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Presentation;

public sealed class CanvasNodeViewModel
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public NodeKind Kind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public string SymbolKey { get; init; } = string.Empty;

    public bool IsRoot { get; init; }

    public bool IsExternal { get; init; }

    public string ExternalCategory { get; init; } = string.Empty;

    public CanvasNodeLane Lane { get; init; } = CanvasNodeLane.Related;

    public int Depth { get; init; }

    public int Order { get; init; }

    public double X { get; init; }

    public double Y { get; init; }

    public double Width { get; init; } = 220d;

    public double Height { get; init; } = 72d;

    public string Accent { get; init; } = string.Empty;

    public IReadOnlyList<UsageMapDetailItem> Details { get; init; } = [];
}
