using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Presentation;

public sealed class UsageMapFilterCriteria
{
    public string SearchText { get; init; } = string.Empty;

    public EdgeKind? EdgeKind { get; init; }

    public NodeKind? NodeKind { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public bool ExcludeExternalSymbols { get; init; }

    public bool ExcludeSystemSymbols { get; init; }

    public bool ExcludePackageSymbols { get; init; }

    public double? MinimumConfidence { get; init; }
}
