using CodeUsageMap.Contracts.Graph;

namespace CodeUsageMap.Contracts.Presentation
{

public sealed class UsageMapNodeViewModel
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public NodeKind Kind { get; init; } = NodeKind.Unknown;

    public string ProjectName { get; init; } = string.Empty;

    public string NamespaceName { get; init; } = string.Empty;

    public string Accessibility { get; init; } = string.Empty;

    public string FilePath { get; init; } = string.Empty;

    public int? LineNumber { get; init; }

    public string SymbolKey { get; init; } = string.Empty;

    public bool IsRoot { get; init; }

    public bool IsExternal { get; init; }

    public string ExternalCategory { get; init; } = string.Empty;

    public IReadOnlyList<UsageMapDetailItem> Details { get; init; } = System.Array.Empty<UsageMapDetailItem>();
}
}
