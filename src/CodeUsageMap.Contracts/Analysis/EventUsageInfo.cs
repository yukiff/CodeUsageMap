using CodeUsageMap.Contracts.Diagnostics;
using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Analysis
{

public sealed class EventUsageInfo
{
    public required string ContainingSymbolId { get; init; }

    public required string ContainingSymbolDisplayName { get; init; }

    public NodeKind ContainingSymbolKind { get; init; } = NodeKind.Method;

    public required string EventSymbolId { get; init; }

    public required string EventName { get; init; }

    public string PublisherTypeName { get; init; } = string.Empty;

    public string HandlerSymbolId { get; init; } = string.Empty;

    public string HandlerName { get; init; } = string.Empty;

    public NodeKind HandlerKind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public EdgeKind Kind { get; init; } = EdgeKind.EventSubscription;

    public AnalysisConfidence Confidence { get; init; } = AnalysisConfidence.High;

    public bool IsUnsubscribed { get; init; }
}
}
